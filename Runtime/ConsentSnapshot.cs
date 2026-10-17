using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 지금의 동의 상태입니다. SDK 에 동의 값을 넘길 때(미디에이션의 사용자 동의, 분석 SDK 의 동의 모드 등) 이 값을 씁니다.
	/// </summary>
	public readonly struct ConsentSnapshot
	{
		private readonly IReadOnlyList<TermsAgreement> _terms;

		public ConsentSnapshot(AdConsentState ads, TrackingStatus tracking, IReadOnlyList<TermsAgreement> terms)
		{
			Ads = ads;
			Tracking = tracking;
			_terms = terms;
		}

		/// <summary>광고 동의. 공급자를 등록하지 않았으면 <see cref="AdConsentState.NotManaged"/></summary>
		public AdConsentState Ads { get; }

		/// <summary>추적 허용. 공급자가 없으면 iOS 는 <see cref="TrackingStatus.NotManaged"/>, 그 밖은 <see cref="TrackingStatus.NotSupported"/></summary>
		public TrackingStatus Tracking { get; }

		/// <summary>추적해도 되는지: ATT 에서 허용했거나 ATT 가 없는 플랫폼</summary>
		public bool TrackingAllowed => Tracking == TrackingStatus.Authorized || Tracking == TrackingStatus.NotSupported;

		/// <summary>약관 항목의 저장된 답(정의 순서). 약관을 등록하지 않았거나 아직 읽지 않았으면 비어 있습니다</summary>
		public IReadOnlyList<TermsAgreement> Terms => _terms ?? Array.Empty<TermsAgreement>();

		public override string ToString()
		{
			return $"ads=({Ads}) tracking={Tracking} terms={Terms.Count}";
		}
	}
}
