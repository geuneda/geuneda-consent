using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 자사 약관 동의입니다. 항목마다 동의 여부·버전·시각을 저장하고, 처음 묻는 항목, 버전이 바뀐 항목, 거절했던 필수 항목만 다시 묻습니다.
	/// 선택 항목을 같은 버전에서 거절한 기록은 다시 묻지 않습니다.
	/// </summary>
	/// <remarks>
	/// <para>메인 스레드에서 씁니다. 저장에 성공한 뒤에만 상태(<see cref="Agreements"/>)를 바꾸고 <see cref="Changed"/> 를 올립니다.</para>
	/// <para>
	/// 한 번에 작업 하나입니다(<see cref="EnsureAsync"/>, <see cref="SetAgreedAsync"/> 가 겹치면 <see cref="ResultCodes.InProgress"/>).
	/// 저장소를 읽기 전에 <see cref="IsAgreed"/> 등을 부르면 <see cref="InvalidOperationException"/> 입니다(먼저 <see cref="EnsureAsync"/>).
	/// </para>
	/// </remarks>
	public sealed class TermsConsent
	{
		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Consent");

		private readonly TermsItem[] _items;
		private readonly ITermsPrompt _prompt;
		private readonly ITermsStore _store;
		private readonly Func<DateTimeOffset> _clock;

		// 저장된 답 전체(정의에서 빠진 예전 항목 포함, 저장소에 그대로 돌려준다)
		private List<TermsAgreement> _records;
		// 정의한 항목의 답만, 정의 순서로(바뀔 때마다 새 배열)
		private TermsAgreement[] _agreements = Array.Empty<TermsAgreement>();
		private readonly Queue<TermsAgreement> _notifications = new Queue<TermsAgreement>();
		private bool _notifying;
		private bool _busy;

		/// <param name="items">항목 정의(정의 순서대로 묻습니다). ID 가 겹치면 예외</param>
		/// <param name="prompt">게임의 동의 화면</param>
		/// <param name="store">저장소. null 이면 <see cref="PlayerPrefsTermsStore"/></param>
		/// <param name="clock">답한 시각. null 이면 기기 시각(<see cref="DateTimeOffset.UtcNow"/>). 서버 시각을 쓰려면 넘깁니다</param>
		public TermsConsent(IReadOnlyList<TermsItem> items, ITermsPrompt prompt, ITermsStore store = null, Func<DateTimeOffset> clock = null)
		{
			if (items == null || items.Count == 0)
			{
				throw new ArgumentException("Terms items are empty", nameof(items));
			}

			var ids = new HashSet<string>(StringComparer.Ordinal);
			_items = new TermsItem[items.Count];

			for (var i = 0; i < items.Count; i++)
			{
				var item = items[i] ?? throw new ArgumentException("Terms items contain null", nameof(items));

				if (!ids.Add(item.Id))
				{
					throw new ArgumentException($"Duplicate terms item id {item.Id}", nameof(items));
				}

				_items[i] = item;
			}

			_prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
			_store = store ?? new PlayerPrefsTermsStore();
			_clock = clock ?? (() => DateTimeOffset.UtcNow);
		}

		/// <summary>답이 저장된 뒤 항목마다 한 번 옵니다(거절·철회 포함). 서버 기록, 광고성 정보 수신 동의 결과 안내에 씁니다</summary>
		public event Action<TermsAgreement> Changed;

		/// <summary>항목 정의(정의 순서)</summary>
		public IReadOnlyList<TermsItem> Items => _items;

		/// <summary>저장소를 읽었는지</summary>
		public bool IsLoaded => _records != null;

		/// <summary>정의한 항목의 저장된 답(정의 순서, 답이 없는 항목은 빠짐). 버전이 지금 정의와 다른 답도 들어 있습니다</summary>
		public IReadOnlyList<TermsAgreement> Agreements
		{
			get
			{
				EnsureLoaded();
				return _agreements;
			}
		}

		/// <summary>그 항목의 지금 버전에 동의했는지</summary>
		public bool IsAgreed(string id)
		{
			var item = GetItem(id);
			EnsureLoaded();
			return TryFind(_agreements, id, out var agreement) && agreement.Version == item.Version && agreement.Agreed;
		}

		/// <summary>그 항목의 저장된 답(버전이 다를 수 있음)</summary>
		public bool TryGetAgreement(string id, out TermsAgreement agreement)
		{
			GetItem(id);
			EnsureLoaded();
			return TryFind(_agreements, id, out agreement);
		}

		/// <summary>
		/// 저장소를 읽고(처음 한 번), 물어야 할 항목이 있으면 동의 화면을 띄워 답을 저장합니다.
		/// </summary>
		/// <returns>
		/// 성공(모든 필수 항목의 지금 버전에 동의함), 또는 실패 코드: <see cref="ConsentResultCodes.TermsDeclined"/>(필수 항목 거절. 답을 저장하지 못했어도 이 코드),
		/// <see cref="ResultCodes.Cancelled"/>, <see cref="ResultCodes.InProgress"/>, 저장소의 실패 코드(답을 저장하지 못하면 상태는 그대로)
		/// </returns>
		public async UniTask<Result> EnsureAsync(CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Result.Fail(ResultCodes.Cancelled);
			}

			if (_busy)
			{
				return Result.Fail(ResultCodes.InProgress, "Another terms operation is in progress");
			}

			// Changed 처리기 안에서는 동의 흐름을 다시 시작하지 않는다. 남은 알림(처리기가 할 철회 등)보다 먼저 결과를 정하게 되기 때문이다.
			// 처리기 안의 SetAgreedAsync 는 허용한다
			if (_notifying)
			{
				return Result.Fail(ResultCodes.InProgress, "EnsureAsync cannot run inside a terms Changed handler");
			}

			_busy = true;
			List<TermsAgreement> decided;
			Result result;

			try
			{
				var load = await LoadIfNeededAsync(cancellationToken);

				if (!load.IsSuccess)
				{
					return load;
				}

				var pending = GetPending();

				if (pending.Count == 0)
				{
					return Result.Ok();
				}

				IReadOnlyDictionary<string, bool> answers;

				try
				{
					answers = await _prompt.ShowAsync(new TermsPromptRequest(pending, GetPrevious(pending)), cancellationToken);
				}
				catch (OperationCanceledException)
				{
					return Result.Fail(ResultCodes.Cancelled);
				}

				decided = Decide(pending, answers);
				var declined = decided.Exists(agreement => !agreement.Agreed && GetItem(agreement.Id).Required);
				var save = await SaveAsync(decided, cancellationToken);

				if (!save.IsSuccess)
				{
					// 필수 항목 거절은 저장하지 못해도 거절로 돌려준다(동의 흐름이 광고 동의·추적 허용으로 넘어가지 않게). 상태는 그대로다
					return declined
						? Result.Fail(ConsentResultCodes.TermsDeclined, $"A required terms item was declined (saving failed: {save.Code} {save.Message})")
						: save;
				}

				result = declined ? Result.Fail(ConsentResultCodes.TermsDeclined, "A required terms item was declined") : Result.Ok();
			}
			finally
			{
				_busy = false;
			}

			// 처리기가 바로 다음 작업을 시작할 수 있게 작업을 끝낸 뒤 알린다
			RaiseChanged(decided);

			if (!result.IsSuccess)
			{
				return result;
			}

			// 처리기가 답을 바꿨을 수 있어 지금 상태로 다시 본다(처리기가 필수 항목을 철회했거나 아직 저장 중이면 흐름을 멈추게)
			if (_busy)
			{
				return Result.Fail(ResultCodes.InProgress, "A terms operation started by a Changed handler is in progress");
			}

			return AllRequiredAgreed() ? result : Result.Fail(ConsentResultCodes.TermsDeclined, "A required terms item was withdrawn by a Changed handler");
		}

		/// <summary>
		/// 한 항목의 답을 바꿉니다(설정 화면의 마케팅 푸시 수신 켜고 끄기 등). 지금 버전으로 같은 답이 이미 있으면 저장하지 않고 성공입니다.
		/// 필수 항목을 철회하면 다음 <see cref="EnsureAsync"/> 가 다시 묻습니다.
		/// </summary>
		/// <returns>성공, 또는 <see cref="ResultCodes.Cancelled"/>, <see cref="ResultCodes.InProgress"/>, 저장소의 실패 코드</returns>
		public async UniTask<Result> SetAgreedAsync(string id, bool agreed, CancellationToken cancellationToken = default)
		{
			var item = GetItem(id);

			if (cancellationToken.IsCancellationRequested)
			{
				return Result.Fail(ResultCodes.Cancelled);
			}

			if (_busy)
			{
				return Result.Fail(ResultCodes.InProgress, "Another terms operation is in progress");
			}

			_busy = true;
			List<TermsAgreement> decided;

			try
			{
				var load = await LoadIfNeededAsync(cancellationToken);

				if (!load.IsSuccess)
				{
					return load;
				}

				if (TryFind(_agreements, id, out var current) && current.Version == item.Version && current.Agreed == agreed)
				{
					return Result.Ok();
				}

				decided = new List<TermsAgreement>(1) { new TermsAgreement(item.Id, item.Version, agreed, _clock()) };
				var save = await SaveAsync(decided, cancellationToken);

				if (!save.IsSuccess)
				{
					return save;
				}
			}
			finally
			{
				_busy = false;
			}

			RaiseChanged(decided);
			return Result.Ok();
		}

		// 물어야 할 항목: 답이 없음, 버전이 다름, 필수인데 거절함
		internal List<TermsItem> GetPending()
		{
			var pending = new List<TermsItem>();

			foreach (var item in _items)
			{
				if (!TryFind(_agreements, item.Id, out var agreement) || agreement.Version != item.Version || item.Required && !agreement.Agreed)
				{
					pending.Add(item);
				}
			}

			return pending;
		}

		private bool AllRequiredAgreed()
		{
			foreach (var item in _items)
			{
				if (item.Required && !(TryFind(_agreements, item.Id, out var agreement) && agreement.Version == item.Version && agreement.Agreed))
				{
					return false;
				}
			}

			return true;
		}

		private async UniTask<Result> LoadIfNeededAsync(CancellationToken cancellationToken)
		{
			if (_records != null)
			{
				return Result.Ok();
			}

			var load = await _store.LoadAsync(cancellationToken);

			if (!load.TryGetValue(out var stored))
			{
				return Result.Fail(load.Code, load.Message);
			}

			// 같은 ID 가 여러 번 있으면 마지막 것을 쓴다
			var records = new List<TermsAgreement>(stored?.Count ?? 0);

			foreach (var agreement in stored ?? Array.Empty<TermsAgreement>())
			{
				var index = records.FindIndex(record => record.Id == agreement.Id);

				if (index >= 0)
				{
					records[index] = agreement;
				}
				else
				{
					records.Add(agreement);
				}
			}

			Apply(records);
			return Result.Ok();
		}

		private Dictionary<string, TermsAgreement> GetPrevious(List<TermsItem> pending)
		{
			var previous = new Dictionary<string, TermsAgreement>(StringComparer.Ordinal);

			foreach (var item in pending)
			{
				if (TryFind(_agreements, item.Id, out var agreement))
				{
					previous[item.Id] = agreement;
				}
			}

			return previous;
		}

		private List<TermsAgreement> Decide(List<TermsItem> pending, IReadOnlyDictionary<string, bool> answers)
		{
			if (answers == null)
			{
				throw new InvalidOperationException("Terms prompt returned null answers");
			}

			foreach (var answer in answers)
			{
				if (!pending.Exists(item => item.Id == answer.Key))
				{
					throw new InvalidOperationException($"Terms prompt answered {answer.Key}, which was not asked");
				}
			}

			var now = _clock();
			var decided = new List<TermsAgreement>(pending.Count);

			foreach (var item in pending)
			{
				decided.Add(new TermsAgreement(item.Id, item.Version, answers.TryGetValue(item.Id, out var agreed) && agreed, now));
			}

			return decided;
		}

		// 저장에 성공했을 때만 상태를 바꾼다
		private async UniTask<Result> SaveAsync(List<TermsAgreement> decided, CancellationToken cancellationToken)
		{
			var records = new List<TermsAgreement>(_records);

			foreach (var agreement in decided)
			{
				var index = records.FindIndex(record => record.Id == agreement.Id);

				if (index >= 0)
				{
					records[index] = agreement;
				}
				else
				{
					records.Add(agreement);
				}
			}

			var save = await _store.SaveAsync(records, cancellationToken);

			if (!save.IsSuccess)
			{
				Logger.Warning($"Saving terms agreements failed ({save.Code}): {save.Message}");
				return save;
			}

			Apply(records);
			return Result.Ok();
		}

		private void Apply(List<TermsAgreement> records)
		{
			var agreements = new List<TermsAgreement>(_items.Length);

			foreach (var item in _items)
			{
				if (TryFind(records, item.Id, out var agreement))
				{
					agreements.Add(agreement);
				}
			}

			_records = records;
			_agreements = agreements.ToArray();
		}

		// 알림 큐: 처리기 안에서 다음 작업이 답을 바꾸면 그 알림은 지금 알림이 모든 구독자에게 간 뒤에 나가, 구독자마다 저장 순서대로 받는다
		private void RaiseChanged(List<TermsAgreement> decided)
		{
			foreach (var agreement in decided)
			{
				_notifications.Enqueue(agreement);
			}

			if (_notifying)
			{
				return;
			}

			_notifying = true;

			try
			{
				while (_notifications.Count > 0)
				{
					var agreement = _notifications.Dequeue();
					var handlers = Changed;

					if (handlers == null)
					{
						continue;
					}

					// 처리기마다 예외를 막아 앞 처리기의 예외가 뒤 처리기(서버 기록 등)의 알림을 막지 않게 한다
					foreach (var handler in handlers.GetInvocationList())
					{
						try
						{
							((Action<TermsAgreement>) handler)(agreement);
						}
						catch (Exception exception)
						{
							Logger.Error(exception, $"A terms Changed handler failed for {agreement.Id}");
						}
					}
				}
			}
			finally
			{
				_notifying = false;
			}
		}

		private TermsItem GetItem(string id)
		{
			foreach (var item in _items)
			{
				if (item.Id == id)
				{
					return item;
				}
			}

			throw new ArgumentException($"Unknown terms item id {id}", nameof(id));
		}

		private void EnsureLoaded()
		{
			if (_records == null)
			{
				throw new InvalidOperationException("Terms agreements are not loaded yet; call EnsureAsync first");
			}
		}

		private static bool TryFind(IReadOnlyList<TermsAgreement> agreements, string id, out TermsAgreement found)
		{
			for (var i = 0; i < agreements.Count; i++)
			{
				if (agreements[i].Id == id)
				{
					found = agreements[i];
					return true;
				}
			}

			found = default;
			return false;
		}
	}
}
