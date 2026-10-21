# SIMBAR 3D source

Blender 5.2.2 LTS에서 제작한 편집 가능한 원본과 재생성 스크립트입니다.

- `SimBarVenue.blend`: 참고 이미지 구도의 벽돌 라이브 바, 가구, 병, 무대, 트러스, 드럼과 소품
- `Gibson_Stage.blend`: 프로젝트의 원래 `Assets/Model/gibson.fbx`를 경량화한 기타
- `SimBarBand.blend`: 기존 FBX를 활용한 4인 밴드, 연주 동작, 본에 연결한 악기와 환경 배경
- `SimBarBand.fbx`: 밴드만 내보낸 교환용 모델 (4개 리그, 74개 메쉬, 120프레임 동작 4개)
- `SimBarBand_GuitarClose_preview.png`: 어깨 스트랩, 지판을 잡는 왼손과 현을 치는 오른손의 근접 렌더
- `SimBarBand_preview.png`: 네 멤버의 얼굴과 연주 자세를 확인한 Blender 렌더
- `SimBarVenue_preview.png`: Blender에서 실제 모델을 렌더링한 검증 이미지
- `*_manifest.json`: 버전, 메쉬 수, 삼각형 수, 재질과 배치 정보

프로젝트 루트에서 PowerShell로 실행합니다. 원본 FBX는 변경하지 않으며 생성 스크립트는 이 폴더의 파생 `.blend`와 `Assets/Resources/SimBarArt`의 파생 FBX를 갱신합니다.

```powershell
& 'E:/Blender/blender.exe' --background --python 'ArtSource/Blender/create_simbar_venue.py'
& 'E:/Blender/blender.exe' --background --python 'ArtSource/Blender/create_gibson_stage.py'
& 'E:/Blender/blender.exe' --background --python 'ArtSource/Blender/create_simbar_band.py'
```

환경 FBX는 10개 정적 메쉬 그룹으로 병합되어 있으며 452,650 triangles입니다. 모든 면을 삼각형으로 정리했습니다. 기타는 원본 485,297 triangles에서 28,465 triangles로 줄였습니다. 원래 기타의 내장 `*0`/`*2` 텍스처 참조를 읽을 수 없어 경량본에는 따뜻한 갈색 마감 재질을 적용했습니다.

밴드 스크립트는 환경과 기타 생성 후 실행합니다. 밴드 FBX는 이 원본 폴더에 저장되며 Unity 게임에서는 개별 FBX와 런타임 동작을 사용하므로 중복 인물이 생성되지 않습니다. 보컬·기타·베이스는 기존 동작을 리타겟하고 드러머는 앉은 스트로크 동작을 사용합니다. 네 멤버가 보이도록 드러머는 무대 후면에 배치했습니다.

기타·베이스는 몸 앞 허리에 걸고 목을 17도 올린 자세로 보정했습니다. 왼쪽 어깨와 등 뒤를 거쳐 양쪽 스트랩 버튼에 연결한 가죽 스트랩을 사용하며, 기타의 실제 변환을 기준으로 팔·손가락·스트럼 동작을 구성했습니다.

환경 제작 스크립트의 좌표 인자는 Unity의 X / 높이 Y / 깊이 Z, 단위는 미터입니다. Blender FBX 축 변환으로 생기는 180도 방향 차이는 Unity의 생성된 `SimBarVenueReady.prefab`에서 한 번 보정합니다.

Unity에서 `SimBar > Art > Import Blender Venue and Build Reference Scene`을 실행하면 URP 재질, FBX 캐릭터·음악 동작 라이브러리, 에디터 프리뷰, `Assets/Scenes/SimBar_Reference.unity`를 생성합니다. 원래 씬은 보존되고 Reference 씬이 빌드 목록 맨 앞에 배치됩니다.
