using System;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 자사 약관 항목 하나입니다(이용약관, 개인정보 수집·이용, 마케팅 푸시 수신, 야간 푸시 수신 등). 내용이 바뀌면 <see cref="Version"/> 을 올립니다.
	/// </summary>
	public sealed class TermsItem
	{
		/// <param name="id">항목 ID. 저장 키로 쓰므로 출시 뒤에는 바꾸지 않습니다</param>
		/// <param name="version">항목 버전(1 이상). 저장된 동의의 버전과 다르면 다시 묻습니다</param>
		/// <param name="required">필수 항목인지. 필수 항목을 거절하면 <see cref="ConsentResultCodes.TermsDeclined"/> 입니다</param>
		public TermsItem(string id, int version, bool required)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				throw new ArgumentException("Terms item id is empty", nameof(id));
			}

			if (version < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(version), version, "Terms item version must be 1 or greater");
			}

			Id = id;
			Version = version;
			Required = required;
		}

		public string Id { get; }

		public int Version { get; }

		public bool Required { get; }

		public override string ToString()
		{
			return $"{Id} v{Version}{(Required ? " (required)" : string.Empty)}";
		}
	}
}
