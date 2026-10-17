# Changelog
이 패키지의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 형식은 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](http://semver.org/spec/v2.0.0.html)을 따릅니다.

## [0.1.0] - 2026-10-17

**New**:
- `IConsentService`/`ConsentService`: 등록한 단계를 자사 약관 → 광고 동의 → 추적 허용 순서로 실행(`RunAsync`). 취소, 필수 약관 거절, 진행 중(단계를 다른 호출이 실행 중)만 멈추고 그 밖의 실패는 다음 단계를 계속 실행한 뒤 첫 실패를 돌려줌(메시지에 단계 이름). 한 번에 작업 하나(`ResultCodes.InProgress`)
- `ConsentSnapshot`: 광고 동의 상태(`AdConsentState`: 광고 요청 가능, TCF 목적 1·3·4 기준 개인화 광고, 개인정보 옵션 버튼 필요, TCF 값), 추적 허용(`TrackingStatus`, `TrackingAllowed`), 약관 답. 공급자가 없으면 광고는 `NotManaged`(막지 않음), 추적은 iOS 면 `NotManaged`·그 밖은 `NotSupported`
- `ShowPrivacyOptionsAsync`: 개인정보 옵션 폼(광고 동의 다시 고르기)
- `TermsConsent`: 항목별(`TermsItem` ID·버전·필수 여부) 자사 약관. 처음 묻는 항목, 버전이 바뀐 항목, 거절했던 필수 항목만 동의 화면(`ITermsPrompt`, 게임 구현)에 넘김. 저장에 성공한 뒤에만 상태를 바꾸고 항목마다 `Changed`(저장 순서대로, 처리기 안의 변경은 지금 알림 뒤, 처리기마다 예외 격리). 필수 항목 거절은 저장에 실패해도 `TermsDeclined`, 알림 처리기가 필수 동의를 철회하면 `TermsDeclined`·아직 저장 중이면 `InProgress`, 처리기 안의 `EnsureAsync` 는 `InProgress`(처리기 안에서는 `SetAgreedAsync` 만). 취소한 토큰은 할 일이 없어도 `Cancelled`. `SetAgreedAsync` 로 설정 화면에서 한 항목 변경. 답한 시각의 시계를 바꿀 수 있음
- `ITermsStore` 와 기본 `PlayerPrefsTermsStore`(JSON, 읽을 수 없는 값은 경고 뒤 없는 것으로 봄, 쓰기 실패는 `TermsSaveFailed`)
- 공급자 계약 `IAdConsentProvider`(UMP 어댑터 com.geuneda.consent.ump), `ITrackingAuthorization`(ATT 어댑터 com.geuneda.consent.att)
- 결과 코드 `ConsentResultCodes`(9000 블록): `TermsDeclined` 9001, `AdConsentUpdateFailed` 9002, `AdConsentFormFailed` 9003, `TermsSaveFailed` 9004
