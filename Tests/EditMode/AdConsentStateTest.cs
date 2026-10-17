using NUnit.Framework;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Tests
{
	public sealed class AdConsentStateTest
	{
		[TestCase(false, null, true, TestName = "NotGdpr_IsPersonalized")]
		[TestCase(true, "1011", true, TestName = "Gdpr_Purposes134_IsPersonalized")]
		[TestCase(true, "1001", false, TestName = "Gdpr_MissingPurpose3_IsNotPersonalized")]
		[TestCase(true, "0011", false, TestName = "Gdpr_MissingPurpose1_IsNotPersonalized")]
		[TestCase(true, "101", false, TestName = "Gdpr_ShortString_IsNotPersonalized")]
		[TestCase(true, null, false, TestName = "Gdpr_NoString_IsNotPersonalized")]
		public void PersonalizedAds_FollowsTcfPurposes134(bool gdpr, string purposes, bool expected)
		{
			var state = new AdConsentState(AdConsentStatus.Obtained, true, false, gdpr, purposes);

			Assert.That(state.PersonalizedAds, Is.EqualTo(expected));
		}

		[Test]
		public void CannotRequestAds_IsNeverPersonalized()
		{
			var state = new AdConsentState(AdConsentStatus.Required, false, false, false, "1111");

			Assert.That(state.PersonalizedAds, Is.False);
		}

		[Test]
		public void HasPurposeConsent_IsOneBased_AndRejectsOutOfRange()
		{
			var state = new AdConsentState(AdConsentStatus.Obtained, true, false, true, "10");

			Assert.That(state.HasPurposeConsent(1), Is.True);
			Assert.That(state.HasPurposeConsent(2), Is.False);
			Assert.That(state.HasPurposeConsent(0), Is.False);
			Assert.That(state.HasPurposeConsent(3), Is.False);
			Assert.That(default(AdConsentState).TcfPurposeConsents, Is.Empty);
			Assert.That(default(AdConsentState).Status, Is.EqualTo(AdConsentStatus.NotManaged));
		}
	}
}
