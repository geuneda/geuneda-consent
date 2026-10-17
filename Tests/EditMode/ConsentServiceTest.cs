using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Tests
{
	public sealed class ConsentServiceTest
	{
		private List<string> _calls;
		private RecordingPrompt _prompt;
		private MemoryTermsStore _store;
		private FakeAdConsent _ads;
		private FakeTracking _tracking;

		[SetUp]
		public void SetUp()
		{
			_calls = new List<string>();
			_prompt = new RecordingPrompt(_calls);
			_store = new MemoryTermsStore();
			_ads = new FakeAdConsent(_calls);
			_tracking = new FakeTracking(_calls);
		}

		private ConsentService Create(bool terms = true, bool ads = true, bool tracking = true, bool platformHasAtt = false)
		{
			return new ConsentService(new ConsentServiceOptions
			{
				Terms = terms ? new TermsConsent(new[] { new TermsItem("tos", 1, true), new TermsItem("push.marketing", 1, false) }, _prompt, _store) : null,
				AdConsent = ads ? _ads : null,
				Tracking = tracking ? _tracking : null
			}, platformHasAtt);
		}

		private static Result Run(ConsentService service)
		{
			var pending = Pending<Result>.Of(service.RunAsync());
			Assert.That(pending.Done, Is.True);
			return pending.Value;
		}

		[Test]
		public void Run_ExecutesTermsThenAdsThenTracking_AndCollectsTheSnapshot()
		{
			var service = Create();
			_ads.StateAfterRequest = new AdConsentState(AdConsentStatus.Obtained, true, true, true, "1111");

			var result = Run(service);

			Assert.That(result.IsSuccess, Is.True, result.ToString());
			Assert.That(_calls, Is.EqualTo(new[] { "terms", "ads", "tracking" }));
			var snapshot = service.Snapshot;
			Assert.That(snapshot.Ads.Status, Is.EqualTo(AdConsentStatus.Obtained));
			Assert.That(snapshot.Ads.PrivacyOptionsRequired, Is.True);
			Assert.That(snapshot.Tracking, Is.EqualTo(TrackingStatus.Authorized));
			Assert.That(snapshot.TrackingAllowed, Is.True);
			Assert.That(snapshot.Terms.Count, Is.EqualTo(2));
			Assert.That(service.Terms.IsAgreed("tos"), Is.True);
		}

		[Test]
		public void SecondRun_SkipsAnsweredTerms_ButRequestsAdConsentAgain()
		{
			var service = Create();
			Run(service);
			_calls.Clear();

			Assert.That(Run(service).IsSuccess, Is.True);
			Assert.That(_calls, Is.EqualTo(new[] { "ads", "tracking" }));
		}

		[Test]
		public void UnregisteredSteps_AreSkipped_AndReportDefaults()
		{
			var service = Create(terms: false, ads: false, tracking: false);

			Assert.That(Run(service).IsSuccess, Is.True);
			Assert.That(_calls, Is.Empty);
			Assert.That(service.Terms, Is.Null);
			var snapshot = service.Snapshot;
			Assert.That(snapshot.Ads.Status, Is.EqualTo(AdConsentStatus.NotManaged));
			Assert.That(snapshot.Ads.CanRequestAds, Is.True);
			Assert.That(snapshot.Ads.PersonalizedAds, Is.True);
			Assert.That(snapshot.Tracking, Is.EqualTo(TrackingStatus.NotSupported));
			Assert.That(snapshot.TrackingAllowed, Is.True);
			Assert.That(snapshot.Terms, Is.Empty);
		}

		[Test]
		public void MissingTrackingProvider_OnAnAttPlatform_IsNotManaged_AndNotAllowed()
		{
			var snapshot = Create(tracking: false, platformHasAtt: true).Snapshot;

			Assert.That(snapshot.Tracking, Is.EqualTo(TrackingStatus.NotManaged));
			Assert.That(snapshot.TrackingAllowed, Is.False);
		}

		[Test]
		public void SnapshotBeforeTheFirstRun_HasNoTerms_AndTheProviderStates()
		{
			_ads.State = new AdConsentState(AdConsentStatus.Obtained, true, false, false, null);
			var snapshot = Create().Snapshot;

			Assert.That(snapshot.Terms, Is.Empty);
			Assert.That(snapshot.Ads.Status, Is.EqualTo(AdConsentStatus.Obtained));
			Assert.That(snapshot.Tracking, Is.EqualTo(TrackingStatus.NotDetermined));
			Assert.That(snapshot.TrackingAllowed, Is.False);
		}

		[Test]
		public void DeclinedRequiredTerms_StopsTheFlow()
		{
			var service = Create();
			_prompt.Answers = new Dictionary<string, bool> { ["tos"] = false };

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.TermsDeclined));
			Assert.That(result.Message, Does.StartWith("terms"));
			Assert.That(_calls, Is.EqualTo(new[] { "terms" }));
		}

		[Test]
		public void CancelledAdConsent_StopsBeforeTracking()
		{
			var service = Create();
			_ads.RequestResult = Result.Fail(ResultCodes.Cancelled);

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(_calls, Is.EqualTo(new[] { "terms", "ads" }));
		}

		[Test]
		public void AdConsentFailure_StillAsksTracking_AndReturnsIt()
		{
			var service = Create();
			_ads.RequestResult = Result.Fail(ConsentResultCodes.AdConsentUpdateFailed, "invalid app id");
			LogAssert.Expect(LogType.Warning, new Regex("Consent step ads failed"));

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.AdConsentUpdateFailed));
			Assert.That(result.Message, Is.EqualTo("ads: invalid app id"));
			Assert.That(_calls, Is.EqualTo(new[] { "terms", "ads", "tracking" }));
			Assert.That(service.Snapshot.Tracking, Is.EqualTo(TrackingStatus.Authorized));
		}

		[Test]
		public void TermsStoreFailure_DoesNotBlockTheOtherSteps_AndTheFirstFailureIsKept()
		{
			var service = Create();
			_store.SaveFailure = Result.Fail(ResultCodes.NetworkError, "offline");
			_ads.RequestResult = Result.Fail(ResultCodes.Timeout, "slow");
			LogAssert.Expect(LogType.Warning, new Regex("Saving terms agreements failed"));
			LogAssert.Expect(LogType.Warning, new Regex("Consent step terms failed"));
			LogAssert.Expect(LogType.Warning, new Regex("Consent step ads failed"));

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.NetworkError));
			Assert.That(result.Message, Is.EqualTo("terms: offline"));
			Assert.That(_calls, Is.EqualTo(new[] { "terms", "ads", "tracking" }));
		}

		[Test]
		public void DeclinedRequiredTerms_ThatCouldNotBeSaved_StillStopsTheFlow()
		{
			var service = Create();
			_prompt.Answers = new Dictionary<string, bool> { ["tos"] = false };
			_store.SaveFailure = Result.Fail(ResultCodes.NetworkError, "offline");
			LogAssert.Expect(LogType.Warning, new Regex("Saving terms agreements failed"));

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.TermsDeclined));
			Assert.That(result.Message, Does.Contain("saving failed"));
			Assert.That(_calls, Is.EqualTo(new[] { "terms" }));
			Assert.That(service.Snapshot.Terms, Is.Empty);
		}

		[Test]
		public void TermsPromptOpenedDirectly_StopsTheFlowWithInProgress()
		{
			var hold = new FakeTermsPrompt { Hold = new UniTaskCompletionSource<IReadOnlyDictionary<string, bool>>() };
			var terms = new TermsConsent(new[] { new TermsItem("tos", 1, true) }, hold, _store);
			var service = new ConsentService(new ConsentServiceOptions { Terms = terms, AdConsent = _ads, Tracking = _tracking }, false);
			var direct = Pending<Result>.Of(terms.EnsureAsync());

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(result.Message, Does.StartWith("terms"));
			Assert.That(_calls, Is.Empty, "ads and tracking wait for the terms answer");

			hold.Hold.TrySetResult(new Dictionary<string, bool> { ["tos"] = true });
			Assert.That(direct.Value.IsSuccess, Is.True);
		}

		[Test]
		public void AdConsentInProgress_StopsBeforeTracking()
		{
			var service = Create();
			_ads.RequestResult = Result.Fail(ResultCodes.InProgress, "a form is open");

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(_calls, Is.EqualTo(new[] { "terms", "ads" }));
		}

		[Test]
		public void HandlerWithdrawingRequiredTerms_StopsBeforeAdsAndTracking()
		{
			var service = Create();
			service.Terms.Changed += agreement =>
			{
				if (agreement.Id == "tos" && agreement.Agreed)
				{
					Pending<Result>.Of(service.Terms.SetAgreedAsync("tos", false));
				}
			};

			var result = Run(service);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.TermsDeclined));
			Assert.That(_calls, Is.EqualTo(new[] { "terms" }));
			Assert.That(service.Terms.IsAgreed("tos"), Is.False);
		}

		[Test]
		public void RunStartedInsideATermsChangedHandler_IsInProgress_AndTheOuterWithdrawalStillApplies()
		{
			var service = Create();
			var ran = new List<Result>();
			service.Terms.Changed += agreement =>
			{
				if (agreement.Id == "push.marketing" && agreement.Agreed && ran.Count == 0)
				{
					ran.Add(Pending<Result>.Of(service.RunAsync()).Value);
				}

				if (agreement.Id == "tos" && agreement.Agreed)
				{
					Pending<Result>.Of(service.Terms.SetAgreedAsync("tos", false));
				}
			};
			_prompt.Answers = new Dictionary<string, bool> { ["tos"] = false };
			Run(service);
			_calls.Clear();
			_prompt.Answers = null;

			Pending<Result>.Of(service.Terms.SetAgreedAsync("push.marketing", true));

			Assert.That(ran, Has.Count.EqualTo(1));
			Assert.That(ran[0].Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(_calls, Is.Empty, "no prompt, ads or tracking from inside the handler");
			Assert.That(service.Terms.IsAgreed("tos"), Is.False);
		}

		[Test]
		public void EnsureInsideAChangedHandler_IsInProgress_EvenWhenNothingIsAsked()
		{
			var service = Create(ads: false, tracking: false);
			Run(service);
			Result? inside = null;
			service.Terms.Changed += agreement => inside = Pending<Result>.Of(service.Terms.EnsureAsync()).Value;

			Pending<Result>.Of(service.Terms.SetAgreedAsync("push.marketing", false));

			Assert.That(inside?.Code, Is.EqualTo(ResultCodes.InProgress));
		}

		[Test]
		public void CancelledToken_ReturnsCancelled_EvenWhenNoStepWouldNotice()
		{
			var cancelled = new System.Threading.CancellationToken(true);

			Assert.That(Pending<Result>.Of(Create(terms: false, ads: false, tracking: false).RunAsync(cancelled)).Value.Code, Is.EqualTo(ResultCodes.Cancelled));

			var answered = Create(ads: false, tracking: false);
			Run(answered);
			_calls.Clear();

			Assert.That(Pending<Result>.Of(answered.RunAsync(cancelled)).Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(_calls, Is.Empty);
		}

		[Test]
		public void OverlappingOperations_ReturnInProgress()
		{
			var service = Create(terms: false);
			_ads.Hold = new UniTaskCompletionSource<Result>();
			var first = Pending<Result>.Of(service.RunAsync());

			Assert.That(Run(service).Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(Pending<Result>.Of(service.ShowPrivacyOptionsAsync()).Value.Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(_ads.PrivacyCalls, Is.EqualTo(0));

			_ads.Hold.TrySetResult(Result.Ok());

			Assert.That(first.Done, Is.True);
			Assert.That(first.Value.IsSuccess, Is.True);
			Assert.That(Run(service).IsSuccess, Is.True, "the busy flag is cleared");
		}

		[Test]
		public void ShowPrivacyOptions_DelegatesToTheProvider()
		{
			var service = Create();
			_ads.PrivacyResult = Result.Fail(ConsentResultCodes.AdConsentFormFailed, "not required");

			var result = Pending<Result>.Of(service.ShowPrivacyOptionsAsync()).Value;

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.AdConsentFormFailed));
			Assert.That(_ads.PrivacyCalls, Is.EqualTo(1));
		}

		[Test]
		public void ShowPrivacyOptions_WithoutAProvider_Throws()
		{
			var service = Create(ads: false);

			Assert.Throws<InvalidOperationException>(() => service.ShowPrivacyOptionsAsync().GetAwaiter().GetResult());
			Assert.Throws<ArgumentNullException>(() => new ConsentService(null));
		}
	}
}
