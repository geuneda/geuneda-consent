using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent
{
	/// <summary>
	/// 약관 동의를 이 기기의 PlayerPrefs 에 JSON 으로 저장합니다. 앱 데이터를 지우면 사라지고(다시 물음), Android 자동 백업·iOS 백업 복원이면 함께 돌아옵니다.
	/// 메인 스레드에서 씁니다. 읽을 수 없는 값은 경고를 남기고 없는 것으로 봅니다(다시 물어 덮어씀).
	/// </summary>
	public sealed class PlayerPrefsTermsStore : ITermsStore
	{
		public const string DefaultKey = "geuneda.consent.terms";

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Consent");

		private readonly string _key;
		private readonly Action<string, string> _write;

		public PlayerPrefsTermsStore(string key = DefaultKey) : this(key, Write)
		{
		}

		internal PlayerPrefsTermsStore(string key, Action<string, string> write)
		{
			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentException("PlayerPrefs key is empty", nameof(key));
			}

			_key = key;
			_write = write;
		}

		public UniTask<Result<IReadOnlyList<TermsAgreement>>> LoadAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return UniTask.FromResult(Result<IReadOnlyList<TermsAgreement>>.Fail(ResultCodes.Cancelled));
			}

			return UniTask.FromResult(Result<IReadOnlyList<TermsAgreement>>.Ok(Parse(PlayerPrefs.GetString(_key, null))));
		}

		public UniTask<Result> SaveAsync(IReadOnlyList<TermsAgreement> agreements, CancellationToken cancellationToken)
		{
			if (agreements == null)
			{
				throw new ArgumentNullException(nameof(agreements));
			}

			if (cancellationToken.IsCancellationRequested)
			{
				return UniTask.FromResult(Result.Fail(ResultCodes.Cancelled));
			}

			var data = new StoredData { items = new List<StoredItem>(agreements.Count) };

			foreach (var agreement in agreements)
			{
				data.items.Add(new StoredItem
				{
					id = agreement.Id,
					version = agreement.Version,
					agreed = agreement.Agreed,
					decidedAtMs = agreement.DecidedAt.ToUnixTimeMilliseconds()
				});
			}

			try
			{
				_write(_key, JsonUtility.ToJson(data));
			}
			catch (PlayerPrefsException exception)
			{
				// WebGL 의 저장 한도(1MB)를 넘은 경우
				Logger.Warning($"Writing terms agreements to PlayerPrefs failed: {exception.Message}");
				return UniTask.FromResult(Result.Fail(ConsentResultCodes.TermsSaveFailed, exception.Message));
			}

			return UniTask.FromResult(Result.Ok());
		}

		private static void Write(string key, string json)
		{
			PlayerPrefs.SetString(key, json);
			// 답을 받은 직후 비정상 종료로 사라지지 않게 바로 쓴다
			PlayerPrefs.Save();
		}

		internal static IReadOnlyList<TermsAgreement> Parse(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				return Array.Empty<TermsAgreement>();
			}

			StoredData data;

			try
			{
				data = JsonUtility.FromJson<StoredData>(json);
			}
			catch (ArgumentException exception)
			{
				Logger.Warning($"Stored terms agreements are not valid JSON, asking again: {exception.Message}");
				return Array.Empty<TermsAgreement>();
			}

			if (data?.items == null)
			{
				return Array.Empty<TermsAgreement>();
			}

			var agreements = new List<TermsAgreement>(data.items.Count);

			foreach (var item in data.items)
			{
				if (item == null || string.IsNullOrWhiteSpace(item.id) || item.version < 1)
				{
					Logger.Warning("Skipping a stored terms agreement without a valid id or version");
					continue;
				}

				DateTimeOffset decidedAt;

				try
				{
					decidedAt = DateTimeOffset.FromUnixTimeMilliseconds(item.decidedAtMs);
				}
				catch (ArgumentOutOfRangeException)
				{
					Logger.Warning($"Skipping stored terms agreement {item.id} with an invalid time {item.decidedAtMs}");
					continue;
				}

				agreements.Add(new TermsAgreement(item.id, item.version, item.agreed, decidedAt));
			}

			return agreements;
		}

		// JsonUtility 형식(필드 이름이 저장 형식이므로 바꾸지 않는다)
		[Serializable]
		private sealed class StoredData
		{
			public List<StoredItem> items;
		}

		[Serializable]
		private sealed class StoredItem
		{
			public string id;
			public int version;
			public bool agreed;
			public long decidedAtMs;
		}
	}
}
