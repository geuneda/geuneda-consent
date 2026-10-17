using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 약관 동의 저장소입니다. 기본은 이 기기의 <see cref="PlayerPrefsTermsStore"/> 이고, 계정에 묶어 서버에 저장하려면 게임이 구현합니다.
	/// 메인 스레드에서 부릅니다. 정상 실패(연결 실패 등)는 결과 코드로, 취소는 <see cref="ResultCodes.Cancelled"/> 로 돌려줍니다.
	/// </summary>
	public interface ITermsStore
	{
		/// <summary>저장된 답을 모두 읽습니다. 저장된 것이 없으면 빈 목록입니다</summary>
		UniTask<Result<IReadOnlyList<TermsAgreement>>> LoadAsync(CancellationToken cancellationToken);

		/// <summary>답 전체를 저장합니다(항목 정의에서 빠진 예전 항목의 답도 넘어옵니다)</summary>
		UniTask<Result> SaveAsync(IReadOnlyList<TermsAgreement> agreements, CancellationToken cancellationToken);
	}
}
