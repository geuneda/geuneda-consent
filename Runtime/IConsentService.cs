using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 앱 시작 때 받는 동의를 정해진 순서(자사 약관 → 광고 동의 → 추적 허용)로 처리합니다. 등록한 단계만 실행합니다.
	/// </summary>
	/// <remarks>
	/// <para>메인 스레드에서 부릅니다. 한 번에 작업 하나입니다(겹치면 <see cref="ResultCodes.InProgress"/>).</para>
	/// <para>미디에이션·분석 SDK 초기화 전에 <see cref="RunAsync"/> 를 끝내고, <see cref="Snapshot"/> 의 값을 SDK 에 넘긴 뒤 초기화합니다.</para>
	/// </remarks>
	public interface IConsentService
	{
		/// <summary>지금 상태(읽을 때마다 공급자에서 새로 모음)</summary>
		ConsentSnapshot Snapshot { get; }

		/// <summary>등록한 자사 약관. 없으면 null</summary>
		TermsConsent Terms { get; }

		/// <summary>
		/// 등록한 단계를 순서대로 실행합니다. 취소, 필수 약관 거절, 진행 중(그 단계를 다른 호출이 아직 실행 중)이면 그 자리에서 멈추고,
		/// 그 밖의 실패는 다음 단계를 계속 실행한 뒤 첫 실패를 돌려줍니다(광고 동의 정보를 받지 못해도 추적 허용은 묻습니다).
		/// 성공이든 실패든 결과는 <see cref="Snapshot"/> 으로 봅니다. 멈춘 코드(취소, 거절, 진행 중)를 받으면 SDK 를 초기화하지 않습니다.
		/// 다시 부르면 이미 답한 단계는 묻지 않고 바로 지나갑니다(광고 동의 정보는 다시 받음).
		/// </summary>
		/// <returns>
		/// 성공, 또는 코드: 멈춤(<see cref="ConsentResultCodes.TermsDeclined"/>, <see cref="ResultCodes.Cancelled"/>, <see cref="ResultCodes.InProgress"/>),
		/// 계속한 첫 실패(광고 동의의 <see cref="ResultCodes.NetworkError"/>, <see cref="ResultCodes.Timeout"/>, <see cref="ConsentResultCodes.AdConsentUpdateFailed"/>,
		/// <see cref="ConsentResultCodes.AdConsentFormFailed"/>, 약관 저장소의 코드와 <see cref="ConsentResultCodes.TermsSaveFailed"/>). 메시지 앞에 단계 이름이 붙습니다
		/// </returns>
		UniTask<Result> RunAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// 개인정보 옵션 폼(광고 동의 다시 고르기)을 띄우고 닫힐 때까지 기다립니다. 광고 동의 공급자가 없으면 <see cref="System.InvalidOperationException"/> 입니다.
		/// 끝나면 바뀐 <see cref="Snapshot"/> 을 SDK 에 다시 넘깁니다
		/// </summary>
		/// <returns>성공, 또는 <see cref="ConsentResultCodes.AdConsentFormFailed"/>, <see cref="ResultCodes.Cancelled"/>, <see cref="ResultCodes.InProgress"/></returns>
		UniTask<Result> ShowPrivacyOptionsAsync(CancellationToken cancellationToken = default);
	}
}
