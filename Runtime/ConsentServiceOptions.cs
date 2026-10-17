// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary><see cref="ConsentService"/> 에 등록할 단계입니다. 등록하지 않은(null) 단계는 건너뜁니다.</summary>
	public sealed class ConsentServiceOptions
	{
		/// <summary>자사 약관. 처음 단계입니다(거절하면 광고 동의와 추적 허용을 묻지 않음)</summary>
		public TermsConsent Terms { get; set; }

		/// <summary>광고 동의(UMP 어댑터 <c>Geuneda.Consent.Ump.UmpAdConsent</c>). 두 번째 단계입니다</summary>
		public IAdConsentProvider AdConsent { get; set; }

		/// <summary>추적 허용(ATT 어댑터 <c>Geuneda.Consent.Att.AttTrackingAuthorization</c>). 마지막 단계입니다(UMP 권장 순서: 광고 동의 폼 뒤 ATT)</summary>
		public ITrackingAuthorization Tracking { get; set; }
	}
}
