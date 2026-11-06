# GitHub Pages 방문자 집계

게임 웹페이지 방문 수는 웹 분석 서비스를 붙여 확인할 수 있습니다. GitHub `Insights → Traffic`은 저장소의 최근14일 조회·고유 방문자·유입 경로 통계입니다. [GitHub 공식 안내](https://docs.github.com/en/repositories/viewing-activity-and-data-for-your-repository/viewing-traffic-to-a-repository)

단순 방문자·페이지 조회 수에는 Cloudflare Web Analytics를 사용할 수 있습니다. 무료이며 DNS를 바꾸지 않고 계정에서 사이트를 추가한 뒤 제공되는 비콘 스니펫을 페이지에 넣는 방식입니다. [Cloudflare 공식 안내](https://developers.cloudflare.com/web-analytics/about/)

최근 접속 현황은 Google Analytics4 실시간 보고서에서 최근5분·30분의 활성 사용자를 확인할 수 있습니다. 이 수치는 해당 시간 범위의 활성 사용자이며 정확한 동시 접속 인원과는 다릅니다. [Google 공식 안내](https://support.google.com/analytics/answer/9271392?hl=en)

이 게임에 연결할 때는 준비 완료(`simbarPlayerReady`) 이후에 분석 스크립트를 비동기로 불러와야 초기 게임 다운로드와 경쟁하지 않습니다. 실제 집계에는 선택한 서비스에서 발급한 사이트 토큰 또는 측정 ID가 필요합니다. 현재는 분석 서비스가 연결되어 있지 않으며, 이 작업에서 방문자를 임의로 집계하거나 외부 서비스에 전송하지 않았습니다.
