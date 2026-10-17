using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Tests
{
	/// <summary>끝났는지와 값을 담습니다. 가짜가 동기로 끝나면 만든 직후 <see cref="Done"/> 입니다.</summary>
	internal sealed class Pending<T>
	{
		public bool Done;
		public T Value;

		public static Pending<T> Of(UniTask<T> task)
		{
			var pending = new Pending<T>();
			Run(task, pending).Forget();
			return pending;
		}

		private static async UniTaskVoid Run(UniTask<T> task, Pending<T> pending)
		{
			pending.Value = await task;
			pending.Done = true;
		}
	}

	/// <summary>답을 미리 정하거나(<see cref="Answers"/>) 열어 둔 채(<see cref="Hold"/>) 요청을 기록합니다.</summary>
	internal sealed class FakeTermsPrompt : ITermsPrompt
	{
		public readonly List<TermsPromptRequest> Requests = new List<TermsPromptRequest>();
		public IReadOnlyDictionary<string, bool> Answers = new Dictionary<string, bool>();
		public UniTaskCompletionSource<IReadOnlyDictionary<string, bool>> Hold;
		public bool Cancel;

		public UniTask<IReadOnlyDictionary<string, bool>> ShowAsync(TermsPromptRequest request, CancellationToken cancellationToken)
		{
			Requests.Add(request);

			if (Cancel)
			{
				throw new OperationCanceledException();
			}

			return Hold != null ? Hold.Task : UniTask.FromResult(Answers);
		}

		public List<string> AskedIds(int request)
		{
			var ids = new List<string>();

			foreach (var item in Requests[request].Items)
			{
				ids.Add(item.Id);
			}

			return ids;
		}
	}

	/// <summary>메모리 저장소. 실패를 흉내 내고 저장한 목록을 기록합니다.</summary>
	internal sealed class MemoryTermsStore : ITermsStore
	{
		public readonly List<List<TermsAgreement>> Saves = new List<List<TermsAgreement>>();
		public List<TermsAgreement> Stored = new List<TermsAgreement>();
		public int LoadCalls;
		public Result? LoadFailure;
		public Result? SaveFailure;
		/// <summary>다음 저장 하나를 열어 둡니다(저장 내용은 바로 기록)</summary>
		public UniTaskCompletionSource<Result> NextSaveHold;

		public UniTask<Result<IReadOnlyList<TermsAgreement>>> LoadAsync(CancellationToken cancellationToken)
		{
			LoadCalls++;

			return UniTask.FromResult(LoadFailure is { } failure
				? Result<IReadOnlyList<TermsAgreement>>.Fail(failure.Code, failure.Message)
				: Result<IReadOnlyList<TermsAgreement>>.Ok(new List<TermsAgreement>(Stored)));
		}

		public UniTask<Result> SaveAsync(IReadOnlyList<TermsAgreement> agreements, CancellationToken cancellationToken)
		{
			if (SaveFailure is { } failure)
			{
				return UniTask.FromResult(failure);
			}

			Stored = new List<TermsAgreement>(agreements);
			Saves.Add(new List<TermsAgreement>(agreements));

			if (NextSaveHold != null)
			{
				var hold = NextSaveHold;
				NextSaveHold = null;
				return hold.Task;
			}

			return UniTask.FromResult(Result.Ok());
		}
	}

	internal sealed class FakeAdConsent : IAdConsentProvider
	{
		private readonly List<string> _calls;

		public FakeAdConsent(List<string> calls)
		{
			_calls = calls;
		}

		public AdConsentState State { get; set; } = new AdConsentState(AdConsentStatus.Unknown, false, false, false, null);

		public Result RequestResult = Result.Ok();
		public AdConsentState? StateAfterRequest;
		public UniTaskCompletionSource<Result> Hold;
		public Result PrivacyResult = Result.Ok();
		public int PrivacyCalls;

		public UniTask<Result> RequestAsync(CancellationToken cancellationToken)
		{
			_calls.Add("ads");

			if (StateAfterRequest is { } state)
			{
				State = state;
			}

			return Hold != null ? Hold.Task : UniTask.FromResult(RequestResult);
		}

		public UniTask<Result> ShowPrivacyOptionsAsync(CancellationToken cancellationToken)
		{
			PrivacyCalls++;
			return Hold != null ? Hold.Task : UniTask.FromResult(PrivacyResult);
		}
	}

	internal sealed class FakeTracking : ITrackingAuthorization
	{
		private readonly List<string> _calls;

		public FakeTracking(List<string> calls)
		{
			_calls = calls;
		}

		public TrackingStatus Status { get; set; } = TrackingStatus.NotDetermined;

		public TrackingStatus StatusAfterRequest = TrackingStatus.Authorized;
		public Result RequestResult = Result.Ok();

		public UniTask<Result> RequestAsync(CancellationToken cancellationToken)
		{
			_calls.Add("tracking");
			Status = StatusAfterRequest;
			return UniTask.FromResult(RequestResult);
		}
	}

	/// <summary>약관 단계 호출 순서를 기록하는 화면(답은 모두 동의)</summary>
	internal sealed class RecordingPrompt : ITermsPrompt
	{
		private readonly List<string> _calls;

		public RecordingPrompt(List<string> calls)
		{
			_calls = calls;
		}

		public IReadOnlyDictionary<string, bool> Answers;

		public UniTask<IReadOnlyDictionary<string, bool>> ShowAsync(TermsPromptRequest request, CancellationToken cancellationToken)
		{
			_calls.Add("terms");

			if (Answers != null)
			{
				return UniTask.FromResult(Answers);
			}

			var answers = new Dictionary<string, bool>();

			foreach (var item in request.Items)
			{
				answers[item.Id] = true;
			}

			return UniTask.FromResult<IReadOnlyDictionary<string, bool>>(answers);
		}
	}
}
