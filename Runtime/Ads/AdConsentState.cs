// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 광고 동의 상태입니다. 공급자(UMP 어댑터)가 SDK 상태와 기기에 저장된 IAB TCF 값을 읽어 만듭니다.
	/// </summary>
	/// <remarks>
	/// 개인화 광고 여부(<see cref="PersonalizedAds"/>)는 TCF 목적 1(기기 저장·접근), 3(개인화 광고 프로필), 4(개인화 광고 선택) 동의로 판정합니다.
	/// 벤더별 동의와 정당한 이익, 미국 주 법(GPP)의 판매 거부는 보지 않습니다. 이 값들은 TCF·GPP 를 지원하는 SDK 가 기기에서 직접 읽습니다.
	/// </remarks>
	public readonly struct AdConsentState
	{
		/// <summary>광고 동의 공급자를 등록하지 않았을 때의 상태입니다. 광고 요청과 개인화 광고를 막지 않습니다</summary>
		public static AdConsentState NotManaged => new AdConsentState(AdConsentStatus.NotManaged, true, false, false, null);

		private readonly string _tcfPurposeConsents;

		/// <param name="status">동의 상태</param>
		/// <param name="canRequestAds">SDK 가 판단한 광고 요청 가능 여부(UMP <c>canRequestAds</c>)</param>
		/// <param name="privacyOptionsRequired">설정 화면에 개인정보 옵션(동의 다시 고르기) 버튼을 둬야 하는지</param>
		/// <param name="gdprApplies">TCF <c>IABTCF_gdprApplies</c> 가 1 인지</param>
		/// <param name="tcfPurposeConsents">TCF <c>IABTCF_PurposeConsents</c>(목적 1 부터 한 글자씩 '1' 동의 '0' 거부). 없으면 null</param>
		public AdConsentState(AdConsentStatus status, bool canRequestAds, bool privacyOptionsRequired, bool gdprApplies, string tcfPurposeConsents)
		{
			Status = status;
			CanRequestAds = canRequestAds;
			PrivacyOptionsRequired = privacyOptionsRequired;
			GdprApplies = gdprApplies;
			_tcfPurposeConsents = tcfPurposeConsents;
		}

		public AdConsentStatus Status { get; }

		/// <summary>광고를 요청해도 되는지. false 면 미디에이션 SDK 초기화와 광고 로드를 미룹니다</summary>
		public bool CanRequestAds { get; }

		/// <summary>설정 화면에 개인정보 옵션 버튼을 둬야 하는지(GDPR 지역에서 동의를 받은 뒤 등). 누르면 <see cref="IConsentService.ShowPrivacyOptionsAsync"/></summary>
		public bool PrivacyOptionsRequired { get; }

		/// <summary>TCF 상 GDPR 이 적용되는지(동의 정보를 받은 적이 없으면 false)</summary>
		public bool GdprApplies { get; }

		/// <summary>TCF 목적별 동의 문자열. 없으면 빈 문자열</summary>
		public string TcfPurposeConsents => _tcfPurposeConsents ?? string.Empty;

		/// <summary>
		/// 개인화 광고를 보여도 되는지입니다. 광고를 요청할 수 있고, GDPR 이 적용되지 않거나 TCF 목적 1·3·4 에 모두 동의했을 때 true 입니다.
		/// </summary>
		public bool PersonalizedAds => CanRequestAds && (!GdprApplies || HasPurposeConsent(1) && HasPurposeConsent(3) && HasPurposeConsent(4));

		/// <summary>TCF 목적(1 부터)에 동의했는지. 문자열이 없거나 짧으면 false</summary>
		public bool HasPurposeConsent(int purpose)
		{
			var consents = TcfPurposeConsents;
			return purpose >= 1 && purpose <= consents.Length && consents[purpose - 1] == '1';
		}

		public override string ToString()
		{
			return $"{Status} canRequestAds={CanRequestAds} privacyOptions={PrivacyOptionsRequired} gdpr={GdprApplies} purposes={TcfPurposeConsents}";
		}
	}
}
