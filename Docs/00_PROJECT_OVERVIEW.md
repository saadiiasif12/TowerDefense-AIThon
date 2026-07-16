# 00 · Project Overview — AI Gaming Hackathon 2026

Source: `AI GAMING HACKATHON 2026 (F).pdf` (email). Summarized here so the project is self-contained.

## Who we are
**Team 49 · GD Main · Category: Tower Defence** · Group: Top Guns · Coach: Junaid Arshad · Coordinators: Arfan Ch, Mahad Nawaz, Laiba Ashraf
Team: Abdullah (Producer), Saad Asif (Developer), Shahbaz (Developer), Wajiha (Artist), Hashir (Artist), Sohail (Concept Artist), Haseeb (Assistant Producer)

## Timeline
| Day | Date | Focus |
|---|---|---|
| Day 1 | Thu 16 July | **CONTROLLER** — core mechanic, input, game feel FIRST |
| Day 2 | Fri 17 July | **CONTENT / LEVELS** — waves, depth, environments |
| Day 3 | Sat 18 July | **POLISH** — VFX, SFX, haptics, bugfix, 5-min video, submission + AI Log |
| Ceremony | Tue 21 July | Winners announced |

## Tower Defence minimum requirements (feed Execution + Content scoring)
- Animation + VFX
- SFX
- **Haptics** (feel requirement)
- **At least 2 environments**
- **5–10 min of gameplay content**

## Judging
| Criterion | Weight | Covers |
|---|---|---|
| Content | 33% | Levels, gameplay depth, playable content vs category minimums |
| Execution | 33% | VFX, SFX, haptics, environments, stability, completeness |
| Creativity | 34% | Originality of concept/art, inventive AI use across the pipeline |

## Compulsory deliverables
1. **The game** (built entirely within the 3 days)
2. **AI Log** — maintained daily from Day 1; every tool, prompts/workflows behind key outputs, what was adopted, usage metrics (tokens/credits). Verified by Coach at check-ins and Check & Balance at submission. → `AILog/AI_LOG.md`
3. **5-minute video** — game concept, gameplay capture, AI workflow highlights (content past 5:00 is not reviewed)
4. **2–3 marketing creatives** (icon, banner, store-style art) — made with AI tools

## Rules that constrain us
- Originality mandatory — clone/near-copy of any existing game (incl. samples: Boom Castle, Evil Tower, Kingdom Rush, We are Warriors) = disqualification
- No pre-built projects/code/assets — everything created during the event; AI-generated content during the event is the point
- AI tool usage is open; recommended stack: Claude Code (code), Scenario (2D), Meshy (3D), Cascadeur/Mixamo (anim), Suno (music), ElevenLabs (SFX/voice), Ludo.ai (UI/UX), Runway (video), Notion AI (production)

## Our standout strategy
- **Mechanical identity:** kill-collection economy (no passive regen) + CR-style card cycle on a TD — not a lane-TD clone
- **Deterministic, tunable core:** all balance in ScriptableObjects → fast on-device iteration; the GDD ships with verified balance numbers
- **Juice budget on Day 3:** orb-flight income feedback, strict-range rings, freeze/lightning spectacle, haptics on every kill/cast
- **AI-first pipeline everywhere** (Meshy models already in project) and an airtight AI Log for the Creativity score
