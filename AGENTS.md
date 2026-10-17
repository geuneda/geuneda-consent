# Geuneda.Consent - AI 에이전트 가이드

> **관련 파일**: `CLAUDE.md`는 이 파일을 감쌉니다 — `CLAUDE.md`가 아니라 `AGENTS.md`를 편집하세요. `README.md`는 사용자 대상 문서입니다.

## 1. 패키지 개요
- **패키지**: `com.geuneda.consent`
- **Unity**: 2021.3+
- **의존성**: `com.geuneda.core` 0.4.0(결과 코드, 로거), `com.cysharp.unitask` 2.5.10, `com.unity.modules.jsonserialize`(PlayerPrefs 저장 형식)
- **역할**: 자사 약관 → 광고 동의 → 추적 허용을 한 곳에서 실행하고 결과 코드와 스냅샷을 줍니다. SDK 호출은 어댑터(`com.geuneda.consent.ump`, `com.geuneda.consent.att`)가 하고, SDK 에 동의 값을 넘기는 것은 게임이 합니다
- **레퍼런스**: Percent `UserAgreement`(UMP·ATT·자사 동의 서비스, UMP 초기화 예외를 코드 1~4 로). 분석은 lucky-defense 레포 `data/analysis/tech/percent/network-auth.md` 4절

## 2. 구조

| 파일 | 역할 |
|------|------|
| `Runtime/IConsentService.cs`, `Runtime/ConsentService.cs` | 실행 순서, 실패 정책, 스냅샷 모으기, 동시 실행 막기 |
| `Runtime/ConsentSnapshot.cs` | SDK 에 넘길 값 |
| `Runtime/Ads/` | 광고 동의 공급자 계약과 상태(TCF 목적으로 개인화 판정) |
| `Runtime/Tracking/` | 추적 허용 공급자 계약과 상태 |
| `Runtime/Terms/TermsConsent.cs` | 자사 약관: 다시 묻기 규칙, 저장 뒤 상태 반영, `Changed` |
| `Runtime/Terms/PlayerPrefsTermsStore.cs` | 기본 저장소(JsonUtility 형식, 필드 이름이 저장 형식) |

## 3. 규칙

- **순서는 고정**(약관 → 광고 동의 → 추적 허용). 약관을 거절한 사용자에게 다른 것을 묻지 않고, UMP 권장대로 광고 동의 폼 뒤에 ATT 를 묻습니다. 순서를 바꾸는 옵션은 두지 않습니다.
- **실패 정책**: 취소(`Cancelled`), 필수 약관 거절(`TermsDeclined`), 진행 중(`InProgress`)만 멈춥니다. 나머지는 다음 단계를 실행하고 첫 실패를 돌려줍니다. 광고 동의 설정 오류(앱 ID 누락 등)가 매번 나도 ATT 를 묻게 하기 위한 것입니다. 진행 중은 실패가 아니라 그 단계를 다른 호출(직접 연 약관 화면, 공급자 직접 호출)이 실행 중이라는 뜻이라, 계속하면 약관 답을 받기 전에 UMP·ATT 를 물어 버립니다(2026-10-17 리뷰 1회차). 서비스의 `_busy` 는 서비스 호출끼리만 막습니다.
- **필수 거절은 저장 실패보다 앞섭니다**: 필수 항목을 거절했으면 저장에 실패해도 `TermsDeclined` 를 돌려줍니다(저장 실패 코드를 돌려주면 서비스가 계속해 거절한 사용자에게 UMP·ATT 를 물음, 리뷰 1회차).
- **스냅샷은 매번 공급자에서 모읍니다**(저장해 둔 값이 낡지 않게). 공급자가 없을 때의 기본값: 광고 `AdConsentState.NotManaged`(요청·개인화 허용), 추적은 iOS 면 `NotManaged`(묻지 않아 IDFA 를 못 씀), 그 밖은 `NotSupported`.
- **약관 상태는 저장에 성공한 뒤에만 바꿉니다.** 저장 실패면 상태와 `Changed` 없이 저장소의 코드를 돌려줍니다. `Changed` 는 작업 표시(`_busy`)를 푼 뒤 올려 처리기가 바로 다음 작업을 시작할 수 있게 합니다.
- **`Changed` 는 알림 큐로 냅니다**(`_notifications`, `_notifying`, ads 의 `Notify`/`Drain` 과 같은 패턴). 처리기 안에서 답을 바꾸면 그 알림은 지금 알림이 모든 구독자에게 간 뒤에 나가, 구독자마다 저장 순서대로 받습니다(바로 올리면 뒤 구독자가 새 답을 먼저, 옛 답을 나중에 받아 저장 상태와 어긋남, 리뷰 1회차). 처리기는 `GetInvocationList()` 로 하나씩 부르고 예외는 처리기마다 Error 로그로 막습니다.
- **기본 저장소의 쓰기 실패**(`PlayerPrefsException`, WebGL 한도)는 `TermsSaveFailed` 결과로 돌려줍니다. 다른 예외는 잡지 않습니다.
- **알림 뒤 결과를 다시 봅니다**: `EnsureAsync` 는 `Changed` 를 보낸 뒤 처리기가 시작한 작업이 진행 중이면 `InProgress`, 필수 항목이 지금 동의 상태가 아니면 `TermsDeclined` 로 바꿉니다(처리기가 필수 동의를 철회했는데 앞서 정한 성공을 돌려주면 서비스가 UMP·ATT 로 넘어감, 리뷰 2회차).
- **처리기 안의 `EnsureAsync` 는 `InProgress`**(`_notifying`): 처리기 안에서 시작한 동의 흐름은 큐에 남은 알림(처리기의 철회 등)보다 먼저 결과를 정해 서비스가 UMP·ATT 로 넘어갈 수 있습니다(리뷰 3회차). 처리기 안의 `SetAgreedAsync` 는 허용합니다.
- **취소는 진입에서 먼저 봅니다**: `RunAsync`(단계마다), `EnsureAsync`, `SetAgreedAsync` 는 묻거나 읽을 것이 없는 경로에서도 취소한 토큰이면 `Cancelled` 입니다(리뷰 2회차).
- **다시 묻기 규칙**: 답 없음, 버전 다름(`!=`, 낮아져도 다시 물음), 필수인데 거절. 선택 항목의 같은 버전 거절은 묻지 않습니다.
- **오용은 예외**: 모르는 항목 ID, 저장소를 읽기 전 조회, 동의 화면이 묻지 않은 항목에 답함·null 답, 광고 동의 공급자 없이 `ShowPrivacyOptionsAsync`. 비동기 메서드 안의 예외는 반환한 `UniTask` 를 기다릴 때 나옵니다.
- **enum 기본값**: `AdConsentStatus.NotManaged`, `TrackingStatus.NotManaged` 가 0 입니다(공급자가 없을 때의 값).
- `PersonalizedAds` 는 TCF 목적 1·3·4 동의만 봅니다(벤더·정당한 이익·GPP 는 SDK 가 직접 읽음). 판정을 넓히면 README 의 정의도 고칩니다.

## 4. 테스트

- EditMode(`Geuneda.Consent.Tests.EditMode`): `ConsentTestDoubles.cs` 의 가짜(동기로 끝나는 동의 화면·저장소·공급자, `Hold` 로 열어 둠)와 `Pending<T>`(동기로 끝난 결과 확인). 순서와 실패 정책, 다시 묻기, 저장·읽기 실패, 동시 실행, 처리기 예외, PlayerPrefs 형식.
- 고친 동작은 되돌리면 실패하는 테스트로 확인합니다(필수 항목 거절 다시 묻기, 실패 뒤 다음 단계 실행은 2026-10-17 에 되돌려 실패를 확인).

## 5. 문서

공개 API 를 바꾸면 `README.md`, `CHANGELOG.md`, 호스트 `claude-skills/geuneda-consent/` 스킬(consent.ump·consent.att 와 공유), 호스트 `claude-skills/geuneda-frameworks/` 를 함께 고칩니다. 결과 코드를 더하면 core `README.md` 의 코드 범위 표(9000 블록)도 봅니다.
