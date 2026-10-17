using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 약관 동의 화면입니다. 게임이 UI 로 구현합니다(문구, 링크, 전체 동의 버튼은 게임 몫).
	/// </summary>
	public interface ITermsPrompt
	{
		/// <summary>
		/// <see cref="TermsPromptRequest.Items"/> 를 보여주고 사용자의 답을 돌려줍니다. 키는 항목 ID, 값은 동의 여부이고 빠진 항목은 거절로 봅니다.
		/// 요청에 없는 ID 를 넣으면 <see cref="System.InvalidOperationException"/> 입니다. 취소되면 <see cref="System.OperationCanceledException"/> 을 던집니다
		/// </summary>
		UniTask<IReadOnlyDictionary<string, bool>> ShowAsync(TermsPromptRequest request, CancellationToken cancellationToken);
	}
}
