# PetTown3D 가벼운 GitHub 업로드

`C:\UnityProjects\UPLOAD_PetTown3D_LIGHT.bat`를 더블클릭하세요.
옆의 `Upload-PetTown3D-Light.ps1`과 이 안내 파일을 함께 보관하세요.
Unity에서 변경한 코드·씬을 저장한 뒤 실행하세요. GitHub 로그인창이 나오면 로그인하세요.

- 저장소: https://github.com/upmppu-lab/PetTown3D
- 업로드 브랜치: `source-lite` (기존 main과 laptop-lite 브랜치는 변경하지 않음)
- 포함: Assets/PetTown3D의 코드, 씬, 프리팹, 머티리얼, 셰이더, 설정과 해당 .meta; ProjectSettings; Packages의 manifest/lock; 텍스트 문서
- 제외: 외부 에셋 팩 폴더, 모델, 이미지·텍스처, 폰트, 오디오, 애니메이션 원본, ZIP/EGG 등 압축파일, APK/AAB, 백업, 캐시, 개별 5MiB 초과 파일
- 확인 가능한 비밀키 패턴을 발견하면 업로드를 중단하지만, 모든 비밀정보를 자동으로 탐지한다고 보장하지는 않음

**이 브랜치는 소스 보관용이며 단독 실행 가능한 전체 게임 프로젝트가 아닙니다.**
다른 PC에서 실행하려면 제외된 에셋과 원래 .meta 파일을 복원해야 합니다.
이 브랜치를 main에 병합하면 에셋 삭제 변경도 포함될 수 있으므로 병합하지 마세요.
큰 파일이 과거 다른 브랜치에 올라갔다면 그 이력을 삭제하지는 않습니다.

업로드 없이 포함 목록과 용량만 확인하려면 명령 프롬프트에서:

```bat
C:\UnityProjects\UPLOAD_PetTown3D_LIGHT.bat -DryRun
```

결과 목록: `C:\UnityProjects\PetTown3D_UploadReports\latest-plan.json`

스크립트를 프로젝트 내부에 두었거나 다른 경로의 프로젝트를 사용한다면:

```bat
UPLOAD_PetTown3D_LIGHT.bat -ProjectPath "C:\UnityProjects\PetTown3D"
```

원본 프로젝트의 파일·현재 브랜치·스테이징·stash를 변경하지 않습니다.
별도 임시 Git 작업 폴더를 만들고 source-lite 브랜치에 일반 push를 수행합니다.
동시에 다른 PC에서 같은 브랜치를 갱신해 push가 거절되면 이 배치 파일을 다시 실행하세요.
실패 시 창의 오류를 확인하세요. 강제 push나 자동 파일 삭제는 하지 않습니다.
임시 업로드 폴더는 확인용으로 남으며 성공 메시지에 경로가 표시됩니다.
