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
	public sealed class TermsConsentTest
	{
		private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 17, 3, 0, 0, TimeSpan.Zero);

		private FakeTermsPrompt _prompt;
		private MemoryTermsStore _store;
		private DateTimeOffset _now;
		private List<TermsAgreement> _changed;

		[SetUp]
		public void SetUp()
		{
			_prompt = new FakeTermsPrompt();
			_store = new MemoryTermsStore();
			_now = T0;
			_changed = new List<TermsAgreement>();
		}

		private TermsConsent Create(params TermsItem[] items)
		{
			if (items.Length == 0)
			{
				items = new[] { new TermsItem("tos", 1, true), new TermsItem("privacy", 1, true), new TermsItem("push.marketing", 1, false) };
			}

			var terms = new TermsConsent(items, _prompt, _store, () => _now);
			terms.Changed += agreement => _changed.Add(agreement);
			return terms;
		}

		private static Result Ensure(TermsConsent terms)
		{
			var pending = Pending<Result>.Of(terms.EnsureAsync());
			Assert.That(pending.Done, Is.True);
			return pending.Value;
		}

		private static Dictionary<string, bool> Answers(params (string id, bool agreed)[] answers)
		{
			var dictionary = new Dictionary<string, bool>();

			foreach (var (id, agreed) in answers)
			{
				dictionary[id] = agreed;
			}

			return dictionary;
		}

		[Test]
		public void FirstRun_AsksEveryItem_SavesAnswers_AndRaisesChangedPerItem()
		{
			var terms = Create();
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", false));

			var result = Ensure(terms);

			Assert.That(result.IsSuccess, Is.True, result.ToString());
			Assert.That(_prompt.AskedIds(0), Is.EqualTo(new[] { "tos", "privacy", "push.marketing" }));
			Assert.That(terms.IsAgreed("tos"), Is.True);
			Assert.That(terms.IsAgreed("push.marketing"), Is.False);
			Assert.That(_store.Saves, Has.Count.EqualTo(1));
			Assert.That(_store.Stored, Has.Count.EqualTo(3));
			Assert.That(_changed, Has.Count.EqualTo(3));
			Assert.That(_changed[2].Agreed, Is.False);
			Assert.That(_changed[2].DecidedAt, Is.EqualTo(T0));
			Assert.That(_changed[2].Version, Is.EqualTo(1));
		}

		[Test]
		public void SecondRun_DoesNotAsk_WhenEveryItemWasAnsweredAtItsVersion()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", false));
			Ensure(Create());
			_changed.Clear();

			var terms = Create();
			var result = Ensure(terms);

			Assert.That(result.IsSuccess, Is.True);
			Assert.That(_prompt.Requests, Has.Count.EqualTo(1), "a declined optional item is not asked again at the same version");
			Assert.That(_store.Saves, Has.Count.EqualTo(1));
			Assert.That(_changed, Is.Empty);
			Assert.That(terms.Agreements.Count, Is.EqualTo(3));
		}

		[Test]
		public void VersionBump_AsksOnlyTheBumpedItem_WithThePreviousAnswer()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", true));
			Ensure(Create());
			_now = T0.AddDays(30);
			_prompt.Answers = Answers(("privacy", true));

			var terms = Create(new TermsItem("tos", 1, true), new TermsItem("privacy", 2, true), new TermsItem("push.marketing", 1, false));
			var result = Ensure(terms);

			Assert.That(result.IsSuccess, Is.True);
			Assert.That(_prompt.AskedIds(1), Is.EqualTo(new[] { "privacy" }));
			Assert.That(_prompt.Requests[1].TryGetPrevious("privacy", out var previous), Is.True);
			Assert.That(previous.Version, Is.EqualTo(1));
			Assert.That(previous.DecidedAt, Is.EqualTo(T0));
			Assert.That(terms.TryGetAgreement("privacy", out var current), Is.True);
			Assert.That(current.Version, Is.EqualTo(2));
			Assert.That(current.DecidedAt, Is.EqualTo(T0.AddDays(30)));
		}

		[Test]
		public void RequiredDeclined_ReturnsTermsDeclined_SavesTheAnswer_AndAsksAgainNextTime()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", false), ("push.marketing", true));
			var terms = Create();

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.TermsDeclined));
			Assert.That(terms.IsAgreed("privacy"), Is.False);
			Assert.That(_store.Stored, Has.Count.EqualTo(3));
			Assert.That(_changed, Has.Count.EqualTo(3));

			_prompt.Answers = Answers(("privacy", true));
			var again = Ensure(Create());

			Assert.That(again.IsSuccess, Is.True);
			Assert.That(_prompt.AskedIds(1), Is.EqualTo(new[] { "privacy" }));
		}

		[Test]
		public void MissingAnswer_IsDeclined()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", true));
			var terms = Create();

			var result = Ensure(terms);

			Assert.That(result.IsSuccess, Is.True);
			Assert.That(terms.TryGetAgreement("push.marketing", out var agreement), Is.True);
			Assert.That(agreement.Agreed, Is.False);
		}

		[Test]
		public void AnswerForAnItemThatWasNotAsked_Throws_AndNothingIsSaved()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", true));
			Ensure(Create());
			var terms = Create(new TermsItem("tos", 2, true), new TermsItem("privacy", 1, true), new TermsItem("push.marketing", 1, false));
			_prompt.Answers = Answers(("tos", true), ("privacy", true));

			Assert.Throws<InvalidOperationException>(() => terms.EnsureAsync().GetAwaiter().GetResult());
			Assert.That(_store.Saves, Has.Count.EqualTo(1));

			_prompt.Answers = null;
			Assert.Throws<InvalidOperationException>(() => terms.EnsureAsync().GetAwaiter().GetResult(), "null answers");

			_prompt.Answers = Answers(("tos", true));
			Assert.That(Ensure(terms).IsSuccess, Is.True, "the busy flag is cleared after a throwing prompt");
		}

		[Test]
		public void CancelledPrompt_ReturnsCancelled_AndKeepsTheState()
		{
			var terms = Create();
			_prompt.Cancel = true;

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(terms.IsLoaded, Is.True);
			Assert.That(terms.Agreements, Is.Empty);
			Assert.That(_store.Saves, Is.Empty);
			Assert.That(_changed, Is.Empty);
		}

		[Test]
		public void SaveFailure_ReturnsTheStoreCode_KeepsTheState_AndAsksAgain()
		{
			var terms = Create();
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", true));
			_store.SaveFailure = Result.Fail(ResultCodes.NetworkError, "offline");
			LogAssert.Expect(LogType.Warning, new Regex("Saving terms agreements failed"));

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.NetworkError));
			Assert.That(terms.Agreements, Is.Empty);
			Assert.That(terms.IsAgreed("tos"), Is.False);
			Assert.That(_changed, Is.Empty);

			_store.SaveFailure = null;
			Assert.That(Ensure(terms).IsSuccess, Is.True);
			Assert.That(_prompt.Requests, Has.Count.EqualTo(2));
			Assert.That(terms.IsAgreed("tos"), Is.True);
		}

		[Test]
		public void LoadFailure_ReturnsTheStoreCode_AndRetriesTheLoadNextTime()
		{
			var terms = Create();
			_store.LoadFailure = Result.Fail(ResultCodes.Timeout, "slow");

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.Timeout));
			Assert.That(terms.IsLoaded, Is.False);
			Assert.That(_prompt.Requests, Is.Empty);
			Assert.Throws<InvalidOperationException>(() => terms.IsAgreed("tos"));

			_store.LoadFailure = null;
			_prompt.Answers = Answers(("tos", true), ("privacy", true));
			Assert.That(Ensure(terms).IsSuccess, Is.True);
			Assert.That(_store.LoadCalls, Is.EqualTo(2));
		}

		[Test]
		public void OverlappingOperations_ReturnInProgress()
		{
			var terms = Create();
			_prompt.Hold = new UniTaskCompletionSource<IReadOnlyDictionary<string, bool>>();
			var first = Pending<Result>.Of(terms.EnsureAsync());

			Assert.That(Ensure(terms).Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", true)).Value.Code, Is.EqualTo(ResultCodes.InProgress));

			_prompt.Hold.TrySetResult(Answers(("tos", true), ("privacy", true)));
			Assert.That(first.Done, Is.True);
			Assert.That(first.Value.IsSuccess, Is.True);
			Assert.That(_prompt.Requests, Has.Count.EqualTo(1));
		}

		[Test]
		public void SetAgreed_ChangesOneItem_AndSkipsTheSameAnswer()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", false));
			var terms = Create();
			Ensure(terms);
			_changed.Clear();
			_now = T0.AddHours(1);

			var opt = Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", true)).Value;

			Assert.That(opt.IsSuccess, Is.True);
			Assert.That(terms.IsAgreed("push.marketing"), Is.True);
			Assert.That(_changed, Has.Count.EqualTo(1));
			Assert.That(_changed[0].DecidedAt, Is.EqualTo(T0.AddHours(1)));
			Assert.That(_store.Saves, Has.Count.EqualTo(2));

			var same = Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", true)).Value;

			Assert.That(same.IsSuccess, Is.True);
			Assert.That(_store.Saves, Has.Count.EqualTo(2));
			Assert.That(_changed, Has.Count.EqualTo(1));
			Assert.Throws<ArgumentException>(() => terms.SetAgreedAsync("unknown", true).GetAwaiter().GetResult());
		}

		[Test]
		public void SetAgreed_LoadsFirst_WhenCalledBeforeEnsure()
		{
			_store.Stored.Add(new TermsAgreement("push.marketing", 1, true, T0));
			var terms = Create();

			var result = Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", false)).Value;

			Assert.That(result.IsSuccess, Is.True);
			Assert.That(terms.IsLoaded, Is.True);
			Assert.That(terms.IsAgreed("push.marketing"), Is.False);
		}

		[Test]
		public void WithdrawingARequiredItem_AsksItAgainOnTheNextEnsure()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", true));
			var terms = Create();
			Ensure(terms);

			Pending<Result>.Of(terms.SetAgreedAsync("privacy", false));
			_prompt.Answers = Answers(("privacy", true));

			Assert.That(Ensure(terms).IsSuccess, Is.True);
			Assert.That(_prompt.AskedIds(1), Is.EqualTo(new[] { "privacy" }));
		}

		[Test]
		public void StaleRecords_ArePassedBackToTheStore()
		{
			_store.Stored.Add(new TermsAgreement("old.event", 3, true, T0));
			_prompt.Answers = Answers(("tos", true), ("privacy", true));
			var terms = Create();

			Ensure(terms);

			Assert.That(_store.Stored.Exists(agreement => agreement.Id == "old.event"), Is.True);
			Assert.That(terms.Agreements.Count, Is.EqualTo(3), "Agreements only holds defined items");
		}

		[Test]
		public void ThrowingChangedHandler_IsLogged_AndLaterHandlersAndAnswersStillArrive()
		{
			var terms = new TermsConsent(new[] { new TermsItem("tos", 1, true), new TermsItem("privacy", 1, true) }, _prompt, _store, () => _now);
			var raised = 0;
			terms.Changed += agreement =>
			{
				raised++;
				throw new InvalidOperationException("handler boom");
			};
			terms.Changed += agreement => _changed.Add(agreement);
			_prompt.Answers = Answers(("tos", true), ("privacy", true));
			LogAssert.Expect(LogType.Error, new Regex("terms Changed handler failed for tos"));
			LogAssert.Expect(LogType.Exception, new Regex("handler boom"));
			LogAssert.Expect(LogType.Error, new Regex("terms Changed handler failed for privacy"));
			LogAssert.Expect(LogType.Exception, new Regex("handler boom"));

			var result = Ensure(terms);

			Assert.That(result.IsSuccess, Is.True);
			Assert.That(raised, Is.EqualTo(2));
			Assert.That(_changed, Has.Count.EqualTo(2), "the handler after the throwing one still receives each answer");
		}

		[Test]
		public void ChangeMadeInsideAHandler_ReachesEverySubscriberAfterTheCurrentChange()
		{
			var terms = new TermsConsent(new[] { new TermsItem("push.marketing", 1, false) }, _prompt, _store, () => _now);
			var first = new List<bool>();
			var second = new List<bool>();
			terms.Changed += agreement =>
			{
				first.Add(agreement.Agreed);

				if (agreement.Agreed)
				{
					Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", false));
				}
			};
			terms.Changed += agreement => second.Add(agreement.Agreed);
			_prompt.Answers = Answers(("push.marketing", true));

			Assert.That(Ensure(terms).IsSuccess, Is.True);

			Assert.That(first, Is.EqualTo(new[] { true, false }));
			Assert.That(second, Is.EqualTo(new[] { true, false }), "the last change a subscriber sees is the saved answer");
			Assert.That(terms.IsAgreed("push.marketing"), Is.False);
		}

		[Test]
		public void HandlerWithdrawingARequiredItem_TurnsTheResultIntoTermsDeclined()
		{
			var terms = new TermsConsent(new[] { new TermsItem("tos", 1, true) }, _prompt, _store, () => _now);
			var seen = new List<bool>();
			terms.Changed += agreement =>
			{
				seen.Add(agreement.Agreed);

				if (agreement.Agreed)
				{
					Pending<Result>.Of(terms.SetAgreedAsync("tos", false));
				}
			};
			_prompt.Answers = Answers(("tos", true));

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.TermsDeclined));
			Assert.That(result.Message, Does.Contain("withdrawn"));
			Assert.That(seen, Is.EqualTo(new[] { true, false }));
			Assert.That(terms.IsAgreed("tos"), Is.False);
		}

		[Test]
		public void HandlerOperationStillSaving_TurnsTheResultIntoInProgress()
		{
			var terms = Create();
			var hold = new UniTaskCompletionSource<Result>();
			Pending<Result> fromHandler = null;
			terms.Changed += agreement =>
			{
				if (agreement.Id == "push.marketing" && fromHandler == null)
				{
					_store.NextSaveHold = hold;
					fromHandler = Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", !agreement.Agreed));
				}
			};
			_prompt.Answers = Answers(("tos", true), ("privacy", true));

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ResultCodes.InProgress));
			Assert.That(fromHandler.Done, Is.False);

			hold.TrySetResult(Result.Ok());
			Assert.That(fromHandler.Value.IsSuccess, Is.True);
			Assert.That(terms.IsAgreed("push.marketing"), Is.True);
		}

		[Test]
		public void CancelledToken_ReturnsCancelled_EvenWhenNothingIsAsked()
		{
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", true));
			var terms = Create();
			Ensure(terms);
			var cancelled = new System.Threading.CancellationToken(true);

			Assert.That(Pending<Result>.Of(terms.EnsureAsync(cancelled)).Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", true, cancelled)).Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(Pending<Result>.Of(Create().EnsureAsync(cancelled)).Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(_store.LoadCalls, Is.EqualTo(1), "a cancelled call does not read the store");
		}

		[Test]
		public void DeclinedRequiredItem_ThatCouldNotBeSaved_IsStillTermsDeclined_AndKeepsTheState()
		{
			var terms = Create();
			_prompt.Answers = Answers(("tos", false), ("privacy", true));
			_store.SaveFailure = Result.Fail(ResultCodes.NetworkError, "offline");
			LogAssert.Expect(LogType.Warning, new Regex("Saving terms agreements failed"));

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.TermsDeclined));
			Assert.That(result.Message, Does.Contain("saving failed: 4 offline"));
			Assert.That(terms.Agreements, Is.Empty);
			Assert.That(_changed, Is.Empty);
		}

		[Test]
		public void DefaultStoreWriteFailure_IsAResult_NotAnException()
		{
			var store = new PlayerPrefsTermsStore("geuneda.consent.terms.unused", (key, json) => throw new PlayerPrefsException("quota"));
			var terms = new TermsConsent(new[] { new TermsItem("tos", 1, true) }, _prompt, store, () => _now);
			terms.Changed += agreement => _changed.Add(agreement);
			_prompt.Answers = Answers(("tos", true));
			LogAssert.Expect(LogType.Warning, new Regex("Writing terms agreements to PlayerPrefs failed: quota"));
			LogAssert.Expect(LogType.Warning, new Regex("Saving terms agreements failed"));

			var result = Ensure(terms);

			Assert.That(result.Code, Is.EqualTo(ConsentResultCodes.TermsSaveFailed));
			Assert.That(terms.IsAgreed("tos"), Is.False);
			Assert.That(_changed, Is.Empty);
		}

		[Test]
		public void ChangedHandler_CanStartTheNextOperation()
		{
			var terms = Create();
			Result? fromHandler = null;
			terms.Changed += agreement =>
			{
				if (agreement.Id == "push.marketing" && !agreement.Agreed)
				{
					fromHandler = Pending<Result>.Of(terms.SetAgreedAsync("push.marketing", true)).Value;
				}
			};
			_prompt.Answers = Answers(("tos", true), ("privacy", true), ("push.marketing", false));

			Ensure(terms);

			Assert.That(fromHandler?.IsSuccess, Is.True);
			Assert.That(terms.IsAgreed("push.marketing"), Is.True);
		}

		[Test]
		public void DuplicateStoredIds_UseTheLastRecord()
		{
			_store.Stored.Add(new TermsAgreement("tos", 1, false, T0));
			_store.Stored.Add(new TermsAgreement("tos", 1, true, T0.AddMinutes(1)));
			var terms = Create(new TermsItem("tos", 1, true));

			Assert.That(Ensure(terms).IsSuccess, Is.True);
			Assert.That(_prompt.Requests, Is.Empty);
			Assert.That(terms.IsAgreed("tos"), Is.True);
		}

		[Test]
		public void Queries_BeforeLoad_Throw_AndUnknownIdsThrow()
		{
			var terms = Create();

			Assert.That(terms.IsLoaded, Is.False);
			Assert.Throws<InvalidOperationException>(() => terms.IsAgreed("tos"));
			Assert.Throws<InvalidOperationException>(() => _ = terms.Agreements);
			Assert.Throws<ArgumentException>(() => terms.IsAgreed("unknown"));
		}

		[Test]
		public void InvalidDefinitions_Throw()
		{
			Assert.Throws<ArgumentException>(() => new TermsConsent(Array.Empty<TermsItem>(), _prompt));
			Assert.Throws<ArgumentException>(() => new TermsConsent(new[] { new TermsItem("a", 1, true), new TermsItem("a", 2, false) }, _prompt));
			Assert.Throws<ArgumentNullException>(() => new TermsConsent(new[] { new TermsItem("a", 1, true) }, null));
			Assert.Throws<ArgumentException>(() => new TermsItem(" ", 1, true));
			Assert.Throws<ArgumentOutOfRangeException>(() => new TermsItem("a", 0, true));
		}
	}
}
