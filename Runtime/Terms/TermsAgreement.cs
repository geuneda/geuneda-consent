using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>약관 항목 하나에 대한 사용자의 답입니다. 저장소에 이 단위로 저장합니다.</summary>
	public readonly struct TermsAgreement
	{
		public TermsAgreement(string id, int version, bool agreed, DateTimeOffset decidedAt)
		{
			Id = id;
			Version = version;
			Agreed = agreed;
			DecidedAt = decidedAt;
		}

		/// <summary>항목 ID(<see cref="TermsItem.Id"/>)</summary>
		public string Id { get; }

		/// <summary>답한 항목의 버전</summary>
		public int Version { get; }

		/// <summary>동의했는지(false 는 거절 또는 철회)</summary>
		public bool Agreed { get; }

		/// <summary>답한 시각. 기본은 기기 시각입니다(<see cref="TermsConsent"/> 의 시계로 바꿈)</summary>
		public DateTimeOffset DecidedAt { get; }

		public override string ToString()
		{
			return $"{Id} v{Version} {(Agreed ? "agreed" : "declined")} at {DecidedAt:O}";
		}
	}
}
