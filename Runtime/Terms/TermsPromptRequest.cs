using System.Collections.Generic;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>동의 화면에 넘기는 요청입니다. 물어야 할 항목과 그 항목의 지난 답(버전이 올랐거나 거절한 기록)을 담습니다.</summary>
	public sealed class TermsPromptRequest
	{
		private readonly IReadOnlyDictionary<string, TermsAgreement> _previous;

		internal TermsPromptRequest(IReadOnlyList<TermsItem> items, IReadOnlyDictionary<string, TermsAgreement> previous)
		{
			Items = items;
			_previous = previous;
		}

		/// <summary>물어야 할 항목(정의한 순서). 처음 묻는 항목, 버전이 바뀐 항목, 거절했던 필수 항목입니다</summary>
		public IReadOnlyList<TermsItem> Items { get; }

		/// <summary>그 항목의 지난 답이 있으면 true(예: 버전이 올라 '약관이 바뀌었습니다'를 보일 때)</summary>
		public bool TryGetPrevious(string id, out TermsAgreement previous)
		{
			return _previous.TryGetValue(id, out previous);
		}
	}
}
