## Skill routing

When the user's request matches an available skill, invoke it via the Skill tool. When in doubt, invoke the skill.

Key routing rules:
- Product ideas/brainstorming → invoke /office-hours
- Strategy/scope → invoke /plan-ceo-review
- Architecture → invoke /plan-eng-review
- Design system/plan review → invoke /design-consultation or /plan-design-review
- Full review pipeline → invoke /autoplan
- Bugs/errors → invoke /investigate
- QA/testing site behavior → invoke /qa or /qa-only
- Code review/diff check → invoke /review
- Visual polish → invoke /design-review
- Ship/deploy/PR → invoke /ship or /land-and-deploy
- Save progress → invoke /context-save
- Resume context → invoke /context-restore
- Author a backlog-ready spec/issue → invoke /spec

## Before finalizing a solution

1. Re-check the root cause.
2. Consider at least 2 alternative solutions.
3. Compare correctness, blast radius, maintainability, and consistency with existing code.
4. Check whether a partial dependency failure can propagate to the entire flow.
5. If a better solution exists, revise before implementation.

## Writing style

- 사용자용 문서는 중학교 2학년이 이해할 수 있는 쉬운 한국어로 쓴다.
- 전문 용어가 꼭 필요하면 처음 나올 때 뜻을 바로 설명한다. 짧은 문장과 구체적인 예를 사용한다.
- 완료한 일, 아직 확인하지 않은 일, 사용자가 할 일을 구분한다. 쉽게 설명하더라도 시험 기준이나 수치를 생략·완화하지 않는다.

## Current measurement constraint

- 2026-09-12: 사용자가 다른 장비를 쓰는 측정은 나중에 진행한다고 했다. 장비 준비를 반복해서 요청하지 않는다.
- 독립적인 코드 작성·자동 시험과 실제 장비 측정을 구분한다. 미측정 항목은 그대로 남기며, 기존 Gate 기준·기간을 완화하거나 전체 Gate 0 통과로 처리하지 않는다.
