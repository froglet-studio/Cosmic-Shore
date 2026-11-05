## What and why

<!-- What the change delivers and why, in a few sentences. -->

## Verification

<!-- What actually ran (gates.sh, unity_refcompile, the editor) and what a person still has to check by hand. -->

## UI checklist

<!-- Answer this section when the PR touches Assets/_Prefabs/UI*, Assets/_Scenes/Menu_Main.unity, GameCanvas,
     the UI theme (Assets/_SO_Assets/UI/) or Assets/_Scripts/UI/. Delete it otherwise.
     Tick each box, or write N/A and why after it. The guide: Docs/UI_STYLE_GUIDE.md (Frogma's Style Guide tab).
     The "UI checklist" workflow fails while a box is unanswered. -->

- [ ] Theme tokens: every colour comes from UIThemeSO / the kit (froglet.css on the web); none typed in by hand
- [ ] Families: each colour means its family's meaning (the colour chart); elements carry no colour
- [ ] Safe area: tested at phone 390x844, iPad 820x1180 and PC 16:9 / 21:9, content inside the safe area
- [ ] Focus: every control reachable with a gamepad and by touch; focus shows the Jade ring
- [ ] Shared prefabs: no scene-level overrides on a shared prefab; changed on the prefab or a variant
- [ ] Words: every player-facing word is an authored field
- [ ] Board: matches its picked Frogma board, or says why not

## Affects

<!-- Whose area this touches (python3 Tools/Build/who_works_where.py query <paths>). -->
