# Gate 1 토스 테스트 빌드

Gate 1의 첫 화면을 WebGL로 만들고, 성공한 출력만 Apps in Toss `.ait` 파일로 묶는다. 이 빌드는 플레이 화면 확인용이며 Gate 0 기기 측정 결과를 대신하지 않는다.

```bash
npm run gate1:build-webgl
npm run gate1:package-toss -- Artifacts/Gate1/<빌드 ID>/WebGL
```

두 번째 명령이 만든 `mallang-brain.ait`를 토스 개발자 도구에 올려 테스트할 수 있다. 화면에서는 **시작**을 누른 뒤, 왼쪽은 `A` 또는 왼쪽 화면, 오른쪽은 `L` 또는 오른쪽 화면으로 연주한다.
