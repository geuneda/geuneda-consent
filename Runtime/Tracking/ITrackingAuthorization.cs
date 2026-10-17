using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 추적 허용 공급자 계약입니다(iOS ATT 는 com.geuneda.consent.att 의 <c>AttTrackingAuthorization</c>). 메인 스레드에서 부릅니다.
	/// </summary>
	public interface ITrackingAuthorization
	{
		/// <summary>지금 상태(기기에 저장된 사용자의 답)</summary>
		TrackingStatus Status { get; }

		/// <summary>
		/// 아직 묻지 않았으면(<see cref="TrackingStatus.NotDetermined"/>) 허용을 묻고 답할 때까지 기다립니다. 이미 답했거나 ATT 가 없는 플랫폼이면 바로 끝납니다.
		/// 답이 무엇이든 성공이고, 답은 <see cref="Status"/> 로 봅니다. 실패는 취소(<see cref="ResultCodes.Cancelled"/>)와 진행 중(<see cref="ResultCodes.InProgress"/>)입니다
		/// </summary>
		UniTask<Result> RequestAsync(CancellationToken cancellationToken);
	}
}
