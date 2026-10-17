using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Tests
{
	public sealed class PlayerPrefsTermsStoreTest
	{
		private const string Key = "geuneda.consent.terms.test";

		[TearDown]
		public void TearDown()
		{
			PlayerPrefs.DeleteKey(Key);
		}

		[Test]
		public void SaveThenLoad_RoundTripsEveryField_AtMillisecondPrecision()
		{
			var store = new PlayerPrefsTermsStore(Key);
			var decidedAt = new DateTimeOffset(2026, 10, 17, 3, 4, 5, 678, TimeSpan.FromHours(9));
			var saved = new List<TermsAgreement>
			{
				new TermsAgreement("tos", 2, true, decidedAt),
				new TermsAgreement("push.night", 1, false, decidedAt.AddDays(1))
			};

			Assert.That(Pending<Geuneda.Core.Result>.Of(store.SaveAsync(saved, default)).Value.IsSuccess, Is.True);
			var loaded = Pending<Geuneda.Core.Result<IReadOnlyList<TermsAgreement>>>.Of(new PlayerPrefsTermsStore(Key).LoadAsync(default)).Value;

			Assert.That(loaded.IsSuccess, Is.True);
			Assert.That(loaded.Value, Has.Count.EqualTo(2));
			Assert.That(loaded.Value[0].Id, Is.EqualTo("tos"));
			Assert.That(loaded.Value[0].Version, Is.EqualTo(2));
			Assert.That(loaded.Value[0].Agreed, Is.True);
			Assert.That(loaded.Value[0].DecidedAt, Is.EqualTo(decidedAt));
			Assert.That(loaded.Value[1].Agreed, Is.False);
		}

		[Test]
		public void MissingKey_LoadsEmpty()
		{
			var loaded = Pending<Geuneda.Core.Result<IReadOnlyList<TermsAgreement>>>.Of(new PlayerPrefsTermsStore(Key).LoadAsync(default)).Value;

			Assert.That(loaded.IsSuccess, Is.True);
			Assert.That(loaded.Value, Is.Empty);
		}

		[Test]
		public void InvalidJson_LoadsEmpty_WithAWarning()
		{
			LogAssert.Expect(LogType.Warning, new Regex("not valid JSON"));

			Assert.That(PlayerPrefsTermsStore.Parse("{not json"), Is.Empty);
		}

		[Test]
		public void InvalidEntries_AreSkipped()
		{
			LogAssert.Expect(LogType.Warning, new Regex("without a valid id or version"));
			LogAssert.Expect(LogType.Warning, new Regex("without a valid id or version"));
			LogAssert.Expect(LogType.Warning, new Regex("invalid time"));

			var parsed = PlayerPrefsTermsStore.Parse(
				"{\"items\":[{\"id\":\"\",\"version\":1},{\"id\":\"a\",\"version\":0},{\"id\":\"b\",\"version\":1,\"decidedAtMs\":9223372036854775807},{\"id\":\"c\",\"version\":3,\"agreed\":true,\"decidedAtMs\":1000}]}");

			Assert.That(parsed, Has.Count.EqualTo(1));
			Assert.That(parsed[0].Id, Is.EqualTo("c"));
			Assert.That(parsed[0].DecidedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1000)));
		}

		[Test]
		public void JsonWithoutItems_LoadsEmpty()
		{
			Assert.That(PlayerPrefsTermsStore.Parse("{}"), Is.Empty);
			Assert.That(PlayerPrefsTermsStore.Parse("{\"items\":[]}"), Is.Empty);
		}
	}
}
