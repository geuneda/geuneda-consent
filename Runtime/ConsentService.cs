using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <inheritdoc />
	/// <remarks>
	/// 순서는 고정입니다. 자사 약관을 먼저 받아 거절한 사용자에게 광고 동의·추적 허용을 묻지 않고, 광고 동의 폼 뒤에 ATT 를 묻습니다(UMP 권장).
	/// 약관을 로그인 때 받으려면 <see cref="ConsentServiceOptions.Terms"/> 에 넣지 말고 그때 <see cref="TermsConsent.EnsureAsync"/> 를 부릅니다.
	/// </remarks>
	public sealed class ConsentService : IConsentService
	{
		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Consent");

		private readonly IAdConsentProvider _adConsent;
		private readonly ITrackingAuthorization _tracking;
		// 공급자가 없을 때 추적 상태: ATT 가 있는 플랫폼이면 묻지 않은 것, 아니면 지원 안 함
		private readonly bool _platformHasAtt;
		private bool _busy;

		public ConsentService(ConsentServiceOptions options) : this(options, Application.platform == RuntimePlatform.IPhonePlayer)
		{
		}

		internal ConsentService(ConsentServiceOptions options, bool platformHasAtt)
		{
			if (options == null)
			{
				throw new ArgumentNullException(nameof(options));
			}

			Terms = options.Terms;
			_adConsent = options.AdConsent;
			_tracking = options.Tracking;
			_platformHasAtt = platformHasAtt;
		}

		public TermsConsent Terms { get; }

		public ConsentSnapshot Snapshot => new ConsentSnapshot(
			_adConsent?.State ?? AdConsentState.NotManaged,
			_tracking?.Status ?? (_platformHasAtt ? TrackingStatus.NotManaged : TrackingStatus.NotSupported),
			Terms != null && Terms.IsLoaded ? Terms.Agreements : null);

		public async UniTask<Result> RunAsync(CancellationToken cancellationToken = default)
		{
			if (_busy)
			{
				return Result.Fail(ResultCodes.InProgress, "Another consent operation is in progress");
			}

			_busy = true;

			try
			{
				var first = Result.Ok();

				// 단계가 취소를 보지 않는 경로(이미 답한 약관, 등록한 단계 없음)에서도 취소한 흐름을 성공으로 끝내지 않게 단계마다 먼저 본다
				if (cancellationToken.IsCancellationRequested)
				{
					return Result.Fail(ResultCodes.Cancelled);
				}

				if (Terms != null)
				{
					var terms = await Terms.EnsureAsync(cancellationToken);

					if (Stops(terms))
					{
						return Named("terms", terms);
					}

					first = Keep(first, "terms", terms);
				}

				if (cancellationToken.IsCancellationRequested)
				{
					return Result.Fail(ResultCodes.Cancelled);
				}

				if (_adConsent != null)
				{
					var ads = await _adConsent.RequestAsync(cancellationToken);

					if (Stops(ads))
					{
						return Named("ads", ads);
					}

					first = Keep(first, "ads", ads);
				}

				if (cancellationToken.IsCancellationRequested)
				{
					return Result.Fail(ResultCodes.Cancelled);
				}

				if (_tracking != null)
				{
					var tracking = await _tracking.RequestAsync(cancellationToken);

					if (Stops(tracking))
					{
						return Named("tracking", tracking);
					}

					first = Keep(first, "tracking", tracking);
				}

				Logger.Info($"Consent flow finished ({(first.IsSuccess ? "ok" : first.Code.ToString())}): {Snapshot}");
				return first;
			}
			finally
			{
				_busy = false;
			}
		}

		public async UniTask<Result> ShowPrivacyOptionsAsync(CancellationToken cancellationToken = default)
		{
			if (_adConsent == null)
			{
				throw new InvalidOperationException("No ad consent provider is registered");
			}

			if (_busy)
			{
				return Result.Fail(ResultCodes.InProgress, "Another consent operation is in progress");
			}

			_busy = true;

			try
			{
				return await _adConsent.ShowPrivacyOptionsAsync(cancellationToken);
			}
			finally
			{
				_busy = false;
			}
		}

		// 취소, 필수 약관 거절, 진행 중(그 단계를 다른 호출이 아직 실행 중, 예: 직접 연 약관 화면)만 흐름을 멈춘다.
		// 나머지 실패는 다음 단계를 막지 않는다(광고 동의 설정 오류가 매번 나도 ATT 는 물음)
		private static bool Stops(Result result)
		{
			return result.Code == ResultCodes.Cancelled || result.Code == ResultCodes.InProgress || result.Code == ConsentResultCodes.TermsDeclined;
		}

		private static Result Keep(Result first, string step, Result result)
		{
			if (result.IsSuccess)
			{
				return first;
			}

			Logger.Warning($"Consent step {step} failed ({result.Code}): {result.Message}");
			return first.IsSuccess ? Named(step, result) : first;
		}

		private static Result Named(string step, Result result)
		{
			return Result.Fail(result.Code, string.IsNullOrEmpty(result.Message) ? step : $"{step}: {result.Message}");
		}
	}
}
