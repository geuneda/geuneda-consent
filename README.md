# Geuneda Consent

앱 시작 때 받는 동의를 정해진 순서로 처리합니다. 등록한 단계만 실행하고, 실패는 결과 코드로 돌려주며, SDK 에 넘길 동의 값을 스냅샷 하나로 모읍니다.

- **순서 고정**: 자사 약관 → 광고 동의(UMP) → 추적 허용(ATT). 약관을 거절한 사용자에게는 광고 동의·추적 허용을 묻지 않고, 광고 동의 폼 뒤에 ATT 를 묻습니다(UMP 권장 순서).
- **실패해도 다음 단계**: 취소, 필수 약관 거절, 진행 중(그 단계를 다른 호출이 실행 중)만 흐름을 멈춥니다. 광고 동의 정보를 받지 못해도(네트워크, 앱 ID 오류) 추적 허용은 묻고, 첫 실패의 코드를 돌려줍니다.
- **자사 약관은 항목별·버전별**: 항목마다 동의 여부·버전·시각을 저장하고, 처음 묻는 항목·버전이 바뀐 항목·거절했던 필수 항목만 다시 묻습니다. 동의 화면은 게임이 만들고, 저장소는 기본이 이 기기(PlayerPrefs)이며 서버 저장은 게임이 붙입니다.
- **스냅샷**: 광고 요청 가능, 개인화 광고, 개인정보 옵션 버튼 필요, 추적 허용, 약관 답을 담습니다. SDK 에 값을 넘기고 초기화하는 것은 게임이 합니다.

광고 동의와 추적 허용은 어댑터 패키지로 붙입니다.

| 패키지 | 내용 |
|---|---|
| `com.geuneda.consent.ump` | Google UMP(사용자 메시지 플랫폼) 광고 동의. Android·iOS 네이티브 브리지 |
| `com.geuneda.consent.att` | iOS 앱 추적 투명성(ATT). 네이티브 브리지와 추적 설명 문구 빌드 후처리 |

## 설치

`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.geuneda.core": "https://github.com/geuneda/geuneda-core.git",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.geuneda.consent": "https://github.com/geuneda/geuneda-consent.git"
  }
}
```

- 의존성: `com.geuneda.core` 0.4.0 이상, UniTask 2.5.10 이상. 최소 Unity 2021.3.

## 사용 예

### 부트스트랩

```csharp
using Geuneda.Consent;
using Geuneda.Core;
using Geuneda.Services;

var terms = new TermsConsent(new[]
{
	new TermsItem("tos", 1, required: true),               // 이용약관
	new TermsItem("privacy", 1, required: true),           // 개인정보 수집·이용
	new TermsItem("push.marketing", 1, required: false),   // 광고성 푸시 수신
	new TermsItem("push.night", 1, required: false)        // 야간(21시~8시) 광고성 푸시 수신
}, new TermsPopupPrompt(uiService));

var consent = new ConsentService(new ConsentServiceOptions
{
	Terms = terms,
	AdConsent = new UmpAdConsent(),              // com.geuneda.consent.ump
	Tracking = new AttTrackingAuthorization()    // com.geuneda.consent.att
});
MainInstaller.Bind<IConsentService>(consent);

var result = await consent.RunAsync(cancellationToken);

if (result.Code == ConsentResultCodes.TermsDeclined)
{
	// 필수 약관 거절: 다시 묻거나 종료 안내
	return;
}

if (result.Code == ResultCodes.Cancelled || result.Code == ResultCodes.InProgress)
{
	// 흐름이 끝나지 않았습니다(시작 흐름 취소, 다른 호출이 동의를 받는 중). SDK 를 초기화하지 않습니다
	return;
}

if (!result.IsSuccess)
{
	// 광고 동의 정보를 못 받는 등. 기기에 남은 지난 결과로 계속합니다
	logger.Warning($"Consent finished with {result.Code}: {result.Message}");
}

// 동의 값을 SDK 에 넘긴 뒤 초기화합니다
var snapshot = consent.Snapshot;
MaxSdk.SetHasUserConsent(snapshot.Ads.PersonalizedAds && snapshot.TrackingAllowed);

if (snapshot.Ads.CanRequestAds)
{
	MaxSdk.InitializeSdk();
}
```

UMP 가 기기에 쓰는 IAB TCF·GPP 문자열은 그 형식을 지원하는 SDK 가 직접 읽습니다. 그 밖의 SDK 에 넘길 값과 API 는 각 SDK 문서를 따릅니다. 위의 `SetHasUserConsent` 값은 레퍼런스 게임(운빨존많겜)이 MAX 에 넘긴 방식(ATT 허용이면서 UMP 개인화)입니다. 레퍼런스는 AppsFlyer 에도 GDPR 사용자면 데이터 사용(TCF 목적 1)과 광고 개인화를 넘겼습니다.

### 동의 화면

`ITermsPrompt` 를 구현합니다. 요청의 항목을 보여주고 항목 ID → 동의 여부를 돌려줍니다. 빠진 항목은 거절로 봅니다.

```csharp
public sealed class TermsPopupPrompt : ITermsPrompt
{
	public async UniTask<IReadOnlyDictionary<string, bool>> ShowAsync(TermsPromptRequest request, CancellationToken cancellationToken)
	{
		foreach (var item in request.Items)
		{
			// 버전이 올라 다시 묻는 항목은 '약관이 바뀌었습니다'
			var changed = request.TryGetPrevious(item.Id, out var previous) && previous.Version != item.Version;
			...
		}

		return await _popup.WaitForAnswersAsync(cancellationToken);   // 취소면 OperationCanceledException
	}
}
```

### 설정 화면

```csharp
// 개인정보 옵션(광고 동의 다시 고르기) 버튼: GDPR 지역에서 동의를 받은 뒤 필요
privacyButton.SetActive(consent.Snapshot.Ads.PrivacyOptionsRequired);

var shown = await consent.ShowPrivacyOptionsAsync(cancellationToken);
MaxSdk.SetHasUserConsent(consent.Snapshot.Ads.PersonalizedAds && consent.Snapshot.TrackingAllowed);

// 광고성 푸시 수신 켜고 끄기(답과 시각이 저장되고 Changed 가 옴)
var toggled = await consent.Terms.SetAgreedAsync("push.marketing", isOn, cancellationToken);
```

### 서버 기록과 수신 동의 결과 안내

`TermsConsent.Changed` 는 답이 저장된 뒤 항목마다 한 번 옵니다(거절·철회 포함). 서버에 기록하거나, 광고성 정보 수신 동의·철회 결과(일시, 처리 내용)를 사용자에게 알릴 때 씁니다. 계정에 묶어 서버에 저장하려면 `ITermsStore` 를 구현해 넘깁니다. 답한 시각은 기본이 기기 시각이고, 서버 시각을 쓰려면 생성자의 `clock` 으로 넘깁니다.

```csharp
terms.Changed += agreement => serverApi.RecordTermsAsync(agreement.Id, agreement.Version, agreement.Agreed, agreement.DecidedAt).Forget();
```

## 동작

### 실행 (`RunAsync`)

| 단계 | 실행 | 실패 |
|---|---|---|
| 자사 약관 | `TermsConsent.EnsureAsync`: 저장소를 읽고(처음 한 번) 물어야 할 항목이 있으면 동의 화면 | 필수 항목 거절(`TermsDeclined`, 답을 저장하지 못했어도), 취소, 진행 중은 멈춤. 저장소 실패는 다음 단계로 |
| 광고 동의 | `IAdConsentProvider.RequestAsync`: 동의 정보를 새로 받고 필요하면 동의 폼 | 취소, 진행 중은 멈춤. 그 밖은 다음 단계로 |
| 추적 허용 | `ITrackingAuthorization.RequestAsync`: 묻지 않았으면 묻고 답을 기다림 | 취소, 진행 중은 멈춤 |

- 진행 중은 그 단계를 다른 호출(직접 연 약관 화면 등)이 아직 실행 중이라는 뜻이라, 답을 받기 전에 다음 단계를 묻지 않게 멈춥니다.
- 결과는 멈춘 코드나 첫 실패의 코드이고, 메시지 앞에 단계 이름(`terms`, `ads`, `tracking`)이 붙습니다. 성공이든 실패든 상태는 `Snapshot` 으로 봅니다.
- 다시 부르면 답한 약관과 이미 답한 ATT 는 묻지 않고 지나갑니다. 광고 동의 정보는 다시 받습니다(UMP 는 앱을 시작할 때마다 받도록 권장).
- 한 번에 작업 하나입니다. 실행 중에 `RunAsync`·`ShowPrivacyOptionsAsync` 를 부르면 `InProgress` 입니다.
- 약관을 로그인 때 받으려면 `ConsentServiceOptions.Terms` 에 넣지 말고 그때 `TermsConsent.EnsureAsync` 를 부릅니다.

### 자사 약관

- 물어야 할 항목: 답이 없음, 저장된 답의 버전이 지금 정의와 다름, 필수 항목인데 거절함. 선택 항목을 같은 버전에서 거절한 답은 다시 묻지 않습니다.
- 동의 화면의 답을 모두 저장한 뒤 필수 항목 중 거절한 것이 있으면 `TermsDeclined` 입니다(거절한 필수 항목은 다음 실행에서 다시 묻습니다).
- 저장에 성공한 뒤에만 상태가 바뀌고 `Changed` 가 옵니다. 저장에 실패하면 상태는 그대로이고 저장소의 코드를 돌려줍니다(다음 실행에서 다시 물음). 필수 항목을 거절했으면 저장에 실패해도 `TermsDeclined` 입니다(메시지에 저장 실패).
- `Changed` 는 저장 순서대로 옵니다. 처리기 안에서 답을 바꾸면 그 알림은 지금 알림이 모든 구독자에게 간 뒤에 옵니다. 처리기의 예외는 Error 로그로 남고 다른 처리기는 그대로 받습니다.
- 알림 뒤 `EnsureAsync` 는 지금 상태로 결과를 다시 정합니다. 처리기가 필수 항목을 철회했으면 `TermsDeclined`, 처리기가 시작한 약관 작업이 아직 저장 중이면 `InProgress` 입니다.
- `Changed` 처리기 안에서는 `SetAgreedAsync` 만 씁니다. 처리기 안에서 부른 `EnsureAsync`(와 그것을 부르는 `RunAsync`)는 `InProgress` 입니다(남은 알림보다 먼저 결과를 정하지 않게).
- 취소한 토큰을 넘기면 묻거나 읽을 것이 없어도 `Cancelled` 입니다(`RunAsync` 도 단계마다 먼저 확인).
- 기본 저장소가 쓰지 못하면(WebGL 저장 한도 등) `TermsSaveFailed` 입니다.
- 정의에서 뺀 예전 항목의 답도 저장소에 그대로 남깁니다.
- `IsAgreed` 는 지금 버전에 동의했는지입니다. 저장소를 읽기 전에 부르면 `InvalidOperationException` 입니다(`EnsureAsync`, `SetAgreedAsync` 가 읽음).
- `SetAgreedAsync` 는 한 항목의 답을 바꿉니다. 지금 버전으로 같은 답이 이미 있으면 저장하지 않습니다. 필수 항목을 철회하면 다음 `EnsureAsync` 가 다시 묻습니다.

### 스냅샷 (`ConsentSnapshot`)

| 값 | 뜻 |
|---|---|
| `Ads.CanRequestAds` | 광고를 요청해도 되는지(UMP `canRequestAds`). 공급자가 없으면 true |
| `Ads.PersonalizedAds` | 광고를 요청할 수 있고, GDPR 이 적용되지 않거나 TCF 목적 1·3·4 에 모두 동의 |
| `Ads.PrivacyOptionsRequired` | 설정 화면에 개인정보 옵션 버튼을 둬야 하는지 |
| `Ads.Status` | `NotManaged`(공급자 없음), `Unknown`, `NotRequired`, `Required`, `Obtained` |
| `Ads.GdprApplies`, `Ads.TcfPurposeConsents`, `Ads.HasPurposeConsent(n)` | 기기에 저장된 TCF 값 |
| `Tracking` | `NotManaged`(iOS 인데 공급자 없음), `NotSupported`(Android·에디터·iOS 14 미만), `NotDetermined`, `Restricted`, `Denied`, `Authorized` |
| `TrackingAllowed` | `Authorized` 이거나 ATT 가 없는 플랫폼 |
| `Terms` | 정의한 항목의 저장된 답(약관이 없거나 아직 읽지 않았으면 비어 있음) |

`PersonalizedAds` 는 벤더별 동의·정당한 이익과 미국 주 법(GPP)의 판매 거부를 보지 않습니다. 이 값들은 TCF·GPP 를 지원하는 SDK 가 직접 읽습니다.

## 결과 코드

| 코드 | 값 | 뜻 |
|---|---|---|
| `ConsentResultCodes.TermsDeclined` | 9001 | 필수 약관 항목 거절 |
| `ConsentResultCodes.AdConsentUpdateFailed` | 9002 | 광고 동의 정보를 받지 못함(앱 ID, 콘솔 메시지 설정, SDK 내부 오류) |
| `ConsentResultCodes.AdConsentFormFailed` | 9003 | 동의 폼·개인정보 옵션 폼을 불러오거나 띄우지 못함 |
| `ConsentResultCodes.TermsSaveFailed` | 9004 | 기본 저장소(PlayerPrefs)가 약관 답을 쓰지 못함 |
| `ResultCodes.NetworkError` | 4 | 광고 동의 정보를 받는 중 연결 실패 |
| `ResultCodes.Timeout` | 3 | 광고 동의 정보 요청 시간 초과 |
| `ResultCodes.Cancelled` | 2 | 취소(동의 화면 취소 포함) |
| `ResultCodes.InProgress` | 5 | 다른 동의 작업이 진행 중 |

## 공급자 만들기

- `IAdConsentProvider`: 요청이 실패해도 `State` 는 기기에 남은 지난 결과를 줍니다(이전 실행의 동의로 광고를 요청할 수 있게). 실패는 위 결과 코드로 돌려줍니다. 이번 실행의 첫 요청 전에는 SDK 가 지난 결과를 아직 읽지 않았을 수 있습니다(UMP 는 Android 에서 `Unknown`, 광고 요청 불가로 확인).
- `ITrackingAuthorization`: `RequestAsync` 는 사용자의 답이 무엇이든 성공입니다(답은 `Status`). 실패는 취소와 진행 중뿐입니다.
- 둘 다 메인 스레드에서 불립니다. SDK 콜백이 다른 스레드에서 오면 메인 스레드로 넘겨 끝냅니다(core `MainThreadDispatcher`).

## 레퍼런스와 다른 점

운빨존많겜(Percent `UserAgreement`)의 구조(UMP·ATT·자사 동의 서비스, 초기화 예외를 결과 코드로)를 따르고 다음을 바꿨습니다.

- 자사 동의 저장값에 항목과 버전이 있습니다(레퍼런스는 동의 여부와 시각 하나뿐이라 약관이 바뀌어도 다시 묻지 못함).
- GDPR 판정을 기기 지역 목록이 아니라 UMP 의 판정(TCF `gdprApplies`)으로 합니다(레퍼런스는 기기 지역이 GDPR 국가 목록에 없으면 UMP 를 부르지 않음).
- 동의 순서를 한 곳에서 실행합니다(레퍼런스는 매니저가 UMP 초기화만 기다리고 폼·ATT 순서는 게임이 짬).
- 개인정보 옵션 폼(동의 다시 고르기)을 지원합니다.

## 한계

- 동의 화면 UI, 약관 문서와 URL, 언어별 문구는 게임이 만듭니다.
- 약관 동의는 기본이 기기 단위입니다. 계정마다 받으려면 계정별 `ITermsStore` 나 키를 씁니다.
- 기본 저장소는 PlayerPrefs 평문입니다(동의 기록은 비밀 값이 아님). 앱 데이터를 지우면 다시 묻습니다.
- 답한 시각의 기본값은 기기 시각입니다. 법적 기록은 서버가 서버 시각으로 남기는 것을 권장합니다.

## 테스트

- `Tests/EditMode`(`Geuneda.Consent.Tests.EditMode`): 가짜 공급자·동의 화면·저장소로 실행 순서와 실패 정책, 약관 다시 묻기 규칙, 저장 실패, 동시 실행, PlayerPrefs 저장 형식.
