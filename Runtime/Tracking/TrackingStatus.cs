// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>iOS 앱 추적 투명성(ATT) 허용 상태입니다. 값은 이 패키지의 값입니다(<c>ATTrackingManagerAuthorizationStatus</c> 와 다름).</summary>
	public enum TrackingStatus
	{
		/// <summary>ATT 가 있는 플랫폼(iOS)인데 추적 허용 공급자를 등록하지 않았습니다. 묻지 않았으므로 광고 식별자를 쓸 수 없습니다</summary>
		NotManaged = 0,

		/// <summary>ATT 가 없는 플랫폼입니다(Android, 에디터, iOS 14 미만). 추적은 ATT 가 아닌 기기 설정을 따릅니다</summary>
		NotSupported = 1,

		/// <summary>아직 묻지 않았거나 물었지만 창이 뜨지 않았습니다</summary>
		NotDetermined = 2,

		/// <summary>기기 정책(자녀 보호 등)으로 물을 수 없습니다</summary>
		Restricted = 3,

		/// <summary>거부했습니다(설정의 '앱이 추적을 요청하도록 허용'이 꺼져 있어도 이 값)</summary>
		Denied = 4,

		/// <summary>허용했습니다</summary>
		Authorized = 5
	}
}
