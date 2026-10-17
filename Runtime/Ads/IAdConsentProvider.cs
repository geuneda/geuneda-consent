using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 광고 동의 공급자 계약입니다(Google UMP 는 com.geuneda.consent.ump 의 <c>UmpAdConsent</c>). 메인 스레드에서 부릅니다.
	/// </summary>
	/// <remarks>
	/// 정상 실패는 결과 코드로 돌려줍니다: 연결 실패 <see cref="ResultCodes.NetworkError"/>, 시간 초과 <see cref="ResultCodes.Timeout"/>,
	/// 취소 <see cref="ResultCodes.Cancelled"/>, 진행 중 <see cref="ResultCodes.InProgress"/>, 그 밖의 SDK 오류 <see cref="ConsentResultCodes"/>.
	/// 실패해도 <see cref="State"/> 는 기기에 남은 지난 결과입니다(이전 실행에서 받은 동의로 광고를 요청할 수 있음).
	/// </remarks>
	public interface IAdConsentProvider
	{
		/// <summary>지금 상태. 요청을 시작한 뒤에는 실패해도 기기에 저장된 지난 결과가 들어 있고, 이번 실행의 첫 요청 전에는 SDK 가 아직 읽지 않아 모를 수 있습니다(UMP Android 는 <see cref="AdConsentStatus.Unknown"/>)</summary>
		AdConsentState State { get; }

		/// <summary>동의 정보를 새로 받고, 동의가 필요하면 동의 폼을 띄워 답을 기다립니다. 앱을 시작할 때마다 부릅니다(SDK 권장)</summary>
		UniTask<Result> RequestAsync(CancellationToken cancellationToken);

		/// <summary>개인정보 옵션 폼(동의 다시 고르기)을 띄우고 닫힐 때까지 기다립니다. <see cref="AdConsentState.PrivacyOptionsRequired"/> 일 때 씁니다</summary>
		UniTask<Result> ShowPrivacyOptionsAsync(CancellationToken cancellationToken);
	}
}
