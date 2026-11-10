# 공연 밴드 모델

이전에 승인된 프리윌 6명·줄라이원츠모어 5명의 시안에서 외형과 편성을 가져와, 실제 게임용 투명 픽셀 스프라이트로 제작했다. 원래 시안은 `references/`에 보관한다. 실명과 얼굴을 검증해 대응한 인물 자료는 아니다.

## 음원 연결

| 밴드 | 트랙 ID | 곡 |
|---|---|---|
| Freewill | track_001, track_004, track_005 | Electric Shocker, Bedroom Manifesto, Electric Shocker - Korean Version |
| Julywantsmore | track_002, track_003 | Hey, No Matter How |

선택된 재생목록의 트랙 ID로 모델을 결정한다. 음원 URL과 저장 구조는 기존 것을 사용하며, 4개의 기존 시뮬레이션 배우에 밴드별 외형과 추가 연주자를 표시한다. 추가 연주자는 부모 기타리스트의 이동·가시성을 따른다.

## 제작 및 재현

- 이미지 생성: 내장 ImageGen. 최종 프롬프트는 [image-prompts.json](image-prompts.json).
- 원본: `freewill.source.png`, `july.source.png`. 프리윌의 최종 채택본은 `freewill.corrected.source.png`.
- 기술 패킹: `Tools/prepare_performing_bands.py`. 투명 여백을 찾아 전체 인물을 자르고, 비율을 유지한 최근접 확대·축소와 발 기준 정렬, PNG 팔레트 압축을 수행한다. 팔다리 합성이나 방향 반전은 하지 않는다.
- 결과: `Tools/WebGLTemplate/FastGame/art/band-freewill.png`, `band-july.png`, `band-models.json`.
- Unity 복제: `Tools/prepare_unity_pixel_art.py`의 `prepare_band_models()`. 동일한 JSON과 아틀라스를 Resources에 넣는다.
- 측정 결과: [packing-report.json](packing-report.json).

64×64 셀, 12열, 발 기준점 (32,60). 연주 4프레임, 이동 4프레임, 대기, 하이파이브, 포옹 자세를 사용한다. 최종 프리윌은 원본 66개 자세를 패킹하고 마지막 열은 포옹 자세를 유지하는 복제 셀이다. 줄라이는 원본 60개 자세다. 두 밴드의 연주·이동 프레임은 각 인물마다 실제로 서로 다르다. 현재 아트는 한 방향의 3/4 시점 스프라이트다.

검사 도구: `Tools/qa_native_band_models.py`. 실제 5곡×3공연장, 앞/뒤 곡, 일시정지, 저장/복원, 미니맵, 공연 후 이동·퇴장과 기존 시뮬레이션의 동등성을 확인한다.
