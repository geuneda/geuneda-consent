// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// consent 패키지의 결과 코드(9000 블록)입니다. 취소는 <see cref="Geuneda.Core.ResultCodes.Cancelled"/>, 다른 작업이 진행 중이면
	/// <see cref="Geuneda.Core.ResultCodes.InProgress"/>, 광고 동의 정보를 받는 중 연결 실패는 <see cref="Geuneda.Core.ResultCodes.NetworkError"/>,
	/// 시간 초과는 <see cref="Geuneda.Core.ResultCodes.Timeout"/> 입니다.
	/// </summary>
	public static class ConsentResultCodes
	{
		/// <summary>필수 약관 항목에 동의하지 않았습니다. 동의 흐름은 여기서 멈춥니다(게임이 다시 묻거나 종료)</summary>
		public const int TermsDeclined = 9001;

		/// <summary>광고 동의 정보를 받지 못했습니다(앱 ID 누락·오류, 콘솔 메시지 설정 오류, SDK 내부 오류). 메시지에 SDK 의 오류가 들어 있습니다</summary>
		public const int AdConsentUpdateFailed = 9002;

		/// <summary>광고 동의 폼이나 개인정보 옵션 폼을 불러오거나 띄우지 못했습니다</summary>
		public const int AdConsentFormFailed = 9003;

		/// <summary>기본 저장소(<see cref="PlayerPrefsTermsStore"/>)가 약관 답을 쓰지 못했습니다(WebGL 저장 한도 등)</summary>
		public const int TermsSaveFailed = 9004;
	}
}
