// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>광고 동의(GDPR 등 개인정보 규정) 상태입니다. 값은 SDK 와 상관없는 이 패키지의 값입니다(UMP 는 Android 와 iOS 의 숫자가 다름).</summary>
	public enum AdConsentStatus
	{
		/// <summary>광고 동의 공급자를 등록하지 않았습니다. 이 패키지는 광고 요청을 막지 않습니다</summary>
		NotManaged = 0,

		/// <summary>아직 모릅니다(이 기기에서 동의 정보를 받은 적이 없음)</summary>
		Unknown = 1,

		/// <summary>이 지역에서는 동의가 필요 없습니다</summary>
		NotRequired = 2,

		/// <summary>동의가 필요한데 아직 받지 못했습니다</summary>
		Required = 3,

		/// <summary>동의 폼에 답했습니다(무엇에 동의했는지는 TCF 문자열로 봅니다)</summary>
		Obtained = 4
	}
}
