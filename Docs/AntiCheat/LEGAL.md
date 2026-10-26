# Fair play: the legal and platform research

**Research, not legal advice.** Gathered 2026-10-22 from primary sources where possible (statute text, regulator
guidance, court rulings, platform documentation). It is written for counsel to check, and counsel signs off before
anything ships. Three markers are used throughout:

- **Interpretation:** our own reading of a source, not something the source says.
- **UNVERIFIED:** a claim we could not confirm. A negative such as "no case found" is also marked UNVERIFIED, since
  not finding something does not prove it does not exist.
- **secondary:** a claim supported only by commentary, not by the primary text.

The design this research shapes is in [`README.md`](README.md).

---

## 1. What this means for the design

1. **The program cannot be secret. How it detects cheats can be.**
   - Anti-cheat data comes from the player's own device, so GDPR Art. 13 applies. Its one exception is that the
     player "already has the information" (Art. 13(4)).
   - The "would seriously impair the purpose" exception exists only in Art. 14(5)(b), which covers data obtained
     from somewhere else. It does not cover this case.
   - Art. 13(2)(f) also requires telling people about automated decision-making.
   - In the US, the FTC treated undisclosed monitoring software as unfair or deceptive in Sony BMG (2007) and
     DesignerWare (2012).
   - So we disclose that the program exists, what it collects and what it can do to an account. We keep the
     signatures and thresholds confidential.
2. **The 3-match silent delay carries the most risk, and how much depends on why the account was flagged.**
   - DSA Recital 55 names "shadow banning" as a restriction. Art. 17(2) requires the statement of reasons "at the
     latest from the date that the restriction is imposed". That clearly reaches restrictions over what a player
     *wrote*. Whether it reaches restrictions over cheat telemetry is arguable.
   - The UK's new Art. 22C requires giving information about significant solely automated decisions.
   - So the design notifies at once for anything a player wrote, keeps the delay only for cheat and bot signals,
     and puts a human in front of anything harsher than the isolated pool. Whether the delay can stay for EU and UK
     players is counsel's question 4 (§4).
3. **Never lock what a player paid for without a way to contest it.**
   - The CPC Network's Key Principles (21 Mar 2025) list as a term to avoid one that lets a trader "ban, suspend or
     remove consumers' access to their accounts or purchased digital content… without the ability to contest the
     reason".
   - The FTC's order against Epic barred locking accounts in a way that cut off purchased content.
4. **A kernel driver or a raw hardware-ID read needs consent in the EU unless it is "strictly necessary".**
   - No EU regulator has ruled on anti-cheat (UNVERIFIED negative).
   - Anti-cheat confined to the game's own process is the most defensible form. Scanning the whole system is the
     least.
   - The UK now lists fraud prevention as strictly necessary (PECR Schedule A1, from 5 Feb 2026), which helps
     there.
5. **Minors change the rules, and the age screen tells us who they are.** The game asks for a birth year
   (`PrivacyConsentOverlay`), so we know who is under 13 and who is under 18.
   - **US (COPPA):** device identifiers, chat and voice are personal information. The amended Rule has been in full
     force since 22 Apr 2026.
   - **India:** a flat ban on "tracking or behavioural monitoring" of under-18s from about 13 May 2027.
   - **EU:** Recital 71 says solely automated decisions "should not concern a child".
   - **UK:** the Age Appropriate Design Code sets profiling off by default unless there is a compelling reason.
   - The design therefore never sends a minor to the isolated pool automatically. A human decides.
6. **"We detected something in your system" is a factual claim.**
   - If the flag came from chat, quitting or a statistical inference, that wording is misleading. The risk arises
     under FTC Act §5, the EU Unfair Commercial Practices Directive and the UK DMCC Act. The DMCC Act has allowed
     direct fines of up to 10% of turnover since 6 Apr 2025.
   - The notice states the real reason category and nothing more.

---

## Part L: the law

### L1. EU GDPR and ePrivacy

**Trust scores, flags and device IDs are personal data.**
- "Online identifier" is in Art. 4(1), and Recital 30 names device and app identifiers.
- CJEU *Nowak* (C-434/16, 20 Dec 2017) held that opinions and assessments about a person are personal data
  (secondary summaries).
- Sources: https://eur-lex.europa.eu/eli/reg/2016/679/oj and https://curia.europa.eu/jcms/jcms/p1_653403/en

**The lawful basis is legitimate interests, not contract.**
- EDPB Guidelines 2/2019 ¶50 say monitoring and profiling for fraud prevention "is likely to go beyond what is
  objectively necessary for the performance of a contract" but may be a legitimate interest.
- Recital 47 covers fraud prevention and Recital 49 covers network and information security.
- Interpretation: legitimate interests, backed by a written balancing test, is the realistic basis.
- This is separate from analytics, which the game runs on opt-in consent.
- Source: https://www.edpb.europa.eu/sites/default/files/files/file1/edpb_guidelines-art_6-1-b-adopted_after_public_consultation_en.pdf

**Art. 22 (solely automated decisions) may apply.**
- It covers decisions "based solely on automated processing" that have legal or "similarly significant" effects.
- WP29's guidelines set the threshold: a decision that "significantly affect[s]… behaviour or choices", has a
  "prolonged or permanent impact", or "lead[s] to the exclusion or discrimination of individuals". The same
  guidelines advise not relying on the Art. 22(2) exceptions for children "as a rule".
- CJEU *SCHUFA* (C-634/21, 7 Dec 2023): an automated score is itself a "decision" when the decision is drawn
  strongly from it.
- Interpretation: isolating someone's matchmaking and locking paid modes plausibly meets the threshold. No regulator
  decision on games was found (UNVERIFIED negative).
- If Art. 22 applies, Art. 22(3) safeguards are owed: human intervention, and the right to state one's view and
  contest the decision.
- Sources: https://ec.europa.eu/newsroom/article29/redirection/document/49826 and
  https://collab.dpa.gr/wp-content/uploads/2023/12/CJEU-C-634-21.en_.pdf

**A player can ask for their flag (Art. 15, right of access).**
- *Danish DPA, Reto Moto (29 Aug 2022):* the company was reprimanded for withholding a player's own chat. It was
  allowed to withhold anti-cheat information, under a national restriction (§22(1) of the Danish Data Protection
  Act), because disclosure "can reveal how players can cheat".
  https://www.edpb.europa.eu/system/files/2023-02/dk_2022-08_decisionpublic_redacted.pdf
- *CJEU C-203/22, Dun & Bradstreet Austria (27 Feb 2025):* the controller must explain the "procedure and principles
  actually applied". If it claims trade secrets, it hands the information to the regulator or court, which does the
  balancing. National rules that exclude trade secrets from access as a rule are precluded.
  https://curia.europa.eu/jcms/upload/docs/application/pdf/2025-02/cp250022en.pdf
- Recital 63: protecting trade secrets "should not be a refusal to provide all information".
- Interpretation: plan to give a player their stage, score and reason category on request, and withhold the
  signatures.

**Reading device information needs consent unless strictly necessary (ePrivacy Art. 5(3)).**
- EDPB Guidelines 2/2023 (v2, adopted 7 Oct 2024) say:
  - Art. 5(3) applies "regardless of whether or not it is personal data";
  - software that sends device data to an API is "access" (¶33);
  - processing that stays on the device is not access until the information "or any derivation" leaves it (¶44);
  - reading a read-only value such as a MAC address needs consent (¶11).
  - The guidelines do not analyse the exemptions.
  - https://www.edpb.europa.eu/system/files/2024-10/edpb_guidelines_202302_technical_scope_art_53_eprivacydirective_v2_en_0.pdf
- WP29 Opinion 04/2012 §3.3 exempts "user centric security" for the security "of the service that has been
  explicitly requested by the user". https://Datenschutz.Hessen.de/sites/datenschutz.hessen.de/files/2022-11/wp_194.pdf
- Interpretation: integrity checks that cover only our own game and our own requests (App Attest, Play Integrity,
  in-process checks) are the defensible form. Hardware serials and system-wide scans are not.

### L2. EU Digital Services Act

**Which category applies depends on the chat surface.**
- A hosting service is defined in Art. 3(g)(iii). An online platform (Art. 3(i)) must disseminate "to the public".
- Recital 14 excludes interpersonal communication "between a finite number of persons determined by the sender"
  from the platform definition, but "public groups or open channels" may be platforms.
- Recital 29 lists VoIP and interpersonal services as mere conduit.
- Interpretation: party and friends chat is not a platform. Open lobby chat may be. Display names shown to
  everyone in the presence lobby are a question for counsel.
- Source: https://eur-lex.europa.eu/eli/reg/2022/2065/oj

**Owed by every intermediary service:**
- Art. 14: the terms must describe moderation, "including algorithmic decision-making and human review".
- Art. 14(4): enforcement must be "diligent, objective and proportionate".
- Art. 14(3): minors must be able to understand the terms.
- Interpretation: this is a legal reason the Code of Conduct must describe the program.

**Statement of reasons (Art. 17):**
- It covers demotion, suspension "of the provision of the service in whole or in part", and account suspension.
- It applies where the restriction is imposed because "information provided by the recipient" is illegal or
  breaks the terms.
- Interpretation: restrictions for toxic names or chat are covered. Restrictions based only on cheat telemetry are
  arguably not.

**Still owed by a small hosting provider:**
- Art. 16: a notice-and-action mechanism.
- Art. 18: promptly inform law enforcement of suspected crimes threatening "life or safety".
- Art. 13: a non-EU provider must name an EU legal representative.

**Exempt for micro and small enterprises (Art. 19):** Section 3, which includes Art. 20 internal complaints,
Art. 28 protection of minors and the statements-of-reasons database. Transparency reports are also exempt
(Art. 15(2)).

**Enforcement against games:** none found (UNVERIFIED negative).

### L3. EU AI Act

- **Social scoring is banned (Art. 5(1)(c), in force 2 Feb 2025).**
  - The ban covers scoring "based on their social behaviour" that leads to detrimental treatment in "unrelated"
    contexts, or treatment "unjustified or disproportionate to their social behaviour or its gravity".
  - Interpretation: a conduct score used only inside the game, for matchmaking, with proportionate consequences, is
    a low but real risk under the "disproportionate" limb. Keep the score inside the game. Never share it or use it
    for anything else.
  - https://eur-lex.europa.eu/eli/reg/2024/1689/oj
- **High-risk systems:** game moderation is not in Annex III. Voice analysis that infers emotion or intent would be
  emotion recognition, which is high-risk under Annex III 1(c) and requires informing the people analysed
  (Art. 50(3)). The design does no voice analysis.
- **Dates:** the research reports a Digital Omnibus on AI, Regulation (EU) 2026/1744 of 8 Jul 2026, which moves the
  Annex III high-risk obligations to 2 Dec 2027 and leaves Art. 50 in place.
  https://eur-lex.europa.eu/eli/reg/2026/1744/oj (counsel to confirm).

### L4. EU consumer law

- **Digital Content Directive 2019/770.**
  - Art. 8(1)(b) requires the "continuity" a consumer may reasonably expect.
  - Art. 19 allows modifying the service only where the contract allows it and gives "a valid reason", with a right
    to terminate if the change is more than minor.
  - https://eur-lex.europa.eu/eli/dir/2019/770/oj
- **Unfair Contract Terms Directive, Annex.** Terms may be unfair where they let the trader:
  - dissolve the contract at its discretion (f);
  - terminate without reasonable notice and without "serious grounds" (g);
  - alter the terms or the service "without a valid reason" (j, k).
  - https://eur-lex.europa.eu/eli/dir/1993/13/oj
- **CPC Key Principles (21 Mar 2025),** non-binding: the "without the ability to contest" term quoted in §1.3.
  https://commission.europa.eu/document/8af13e88-6540-436c-b137-9853e7fe866a_en
- **Who is the "trader"** when the sale goes through Steam or an app store: UNVERIFIED.
- **The Digital Fairness Act** proposal was expected in late 2026; whether it has been published is UNVERIFIED.

### L5. United Kingdom

- **The Online Safety Act applies** to games where users chat (text or voice) or are matched with each other
  (Ofcom gaming guidance, Oct 2025).
  https://www.lewissilkin.com/insights/2025/10/24/ofcom-issues-guidance-about-online-safety-act-in-video-gaming-context-102lr5v
- **Its duties are already in force:**
  - illegal-content risk assessment due 16 Mar 2025;
  - children's access assessment due 16 Apr 2025;
  - children's risk assessment due 24 Jul 2025.
  - https://www.ofcom.org.uk/online-safety/illegal-and-harmful-content/important-dates-for-online-safety-compliance
- **Complaints procedures (s.21)** must cover:
  - users whose content is taken down;
  - users "warned, suspended, banned" because their content was considered illegal (or harmful to children);
  - misuse of proactive technology.
  - Restrictions for cheating alone are not listed.
  - https://www.legislation.gov.uk/ukpga/2023/50/section/21
- **UK GDPR Arts. 22A-22D** (Data (Use and Access) Act 2025, commenced 5 Feb 2026): significant solely automated
  decisions are allowed only with safeguards, including providing "information about decisions".
  https://www.legislation.gov.uk/ukpga/2025/18/section/80
- **PECR Schedule A1 ¶4(2)** (from 5 Feb 2026) lists "preventing or detecting fraud" connected to the service as
  strictly necessary. Interpretation: UK anti-cheat device reads are on firmer ground than EU ones.
  https://www.legislation.gov.uk/uksi/2003/2426/schedule/A1
- **Age Appropriate Design Code, standard 12:** profiling is off by default unless there is a "compelling reason".
  Security and crime prevention are named as examples.
  https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/childrens-information/childrens-code-guidance-and-resources/age-appropriate-design-a-code-of-practice-for-online-services/12-profiling/

### L6. US federal

- **FTC v. Epic Games (19 Dec 2022).**
  - $275M COPPA penalty and $245M in refunds.
  - Voice and text chat switched on by default for children and teens was charged as unfair.
  - Epic "locked the accounts of customers who disputed unauthorized charges". The order bars this.
  - https://www.ftc.gov/news-events/news/press-releases/2022/12/fortnite-video-game-maker-epic-games-pay-more-half-billion-dollars-over-ftc-allegations
- **Sony BMG (FTC settlement, 30 Jan 2007).** Hidden monitoring software that was hard to uninstall. The settlement
  barred installing software without consent and required reimbursing repairs. Texas and California state actions
  followed.
  https://www.theregister.com/2007/01/31/ftc_settlement/
- **DesignerWare (2012).** Undisclosed monitoring software was charged as unfair.
  https://www.ftc.gov/news-events/news/press-releases/2012/09/ftc-halts-computer-spying
- **FTC action on disclosed anti-cheat:** none found (UNVERIFIED negative).
- **COPPA, amended Rule.**
  - Published 22 Apr 2025, effective 23 Jun 2025, compliance date 22 Apr 2026.
  - Personal information includes persistent identifiers (including a device serial number), audio of a child's
    voice, and, newly, voiceprints.
  - The **internal-operations exception** covers protecting "security or integrity" without parental consent. It
    forbids building a profile for any other purpose and requires notice (§312.4(d)(3)). This is how anti-cheat
    covers under-13 players.
  - The audio exception does not cover moderating voice chat.
  - Mixed-audience services need a neutral age screen, which the game has.
  - https://www.ecfr.gov/current/title-16/chapter-I/subchapter-C/part-312
- **Section 230(c)(2)(A)** protects action "voluntarily taken in good faith to restrict access to or availability of
  material". Interpretation: it fits chat moderation. Whether it covers restrictions for cheating, or covert ones, is
  uncertain. https://www.law.cornell.edu/uscode/text/47/230

### L7. US states

- **CCPA/CPRA.**
  - Covered businesses: the revenue threshold is $26,625,000 from 1 Jan 2025 (other thresholds are in
    §1798.140(d)). Froglet may not be covered yet; counsel to check.
    https://www.cppa.ca.gov/regulations/cpi_adjustment.html
  - The right to know includes "internally generated inferences", but not the trade secrets that produce them
    (AG Opinion 20-303, 2022; secondary).
  - The automated decision-making (ADMT) rules take effect 1 Jan 2026, with compliance due 1 Jan 2027. Their
    "significant decision" list (financial, housing, education, employment, healthcare) does not reach games
    (interpretation).
- **Wiretap law (CIPA) and AI analysis of voice.** *Ambriz v. Google* (N.D. Cal., 10 Feb 2025) let a claim proceed
  against an AI vendor analysing calls. No suit against a game's voice moderation was found (UNVERIFIED negative).
  Interpretation: no voice chat, no voice analysis.
- **Illinois BIPA** covers voiceprints, at $1,000 or $5,000 per violation (secondary).
- **California Age-Appropriate Design Code:** after the Ninth Circuit's 12 Mar 2026 ruling, most of the code stays
  enjoined (secondary).

### L8. Computer misuse and spyware

- **UK Computer Misuse Act s.17(8):** an act is unauthorised without "consent to the act". Informed consent is the
  defence. Functions beyond what was disclosed are the risk.
- **CFAA:** turns on "without authorization".
- **California's spyware law** targets software installed by "intentionally deceptive means" (secondary).
- Interpretation: a *disclosed* client anti-cheat with consent at install is lawful. An *undisclosed* one is the
  Sony BMG case.

### L9. India

- **DPDP Act 2023 and Rules (G.S.R. 846(E), 13 Nov 2025).** Most obligations apply from about 13 May 2027.
  - There is no general legitimate-interests basis: consent or listed "legitimate uses" only (s.4).
  - A child is anyone under 18 (s.2(f)). Processing a child's data needs verifiable parental consent (s.9(1)).
  - s.9(3): "shall not undertake tracking or behavioural monitoring of children". Whether the s.17(1)(a)/(c)
    exemptions (enforcing legal rights, preventing contraventions of law) cover anti-cheat is UNVERIFIED.
  - s.17(1)(d) exempts collaborators in India who process *non-Indian* players' data under contract with a foreign
    company.
  - https://www.meity.gov.in/static/uploads/2025/11/53450e6e5dc0bfa85ebd78686cadad39.pdf
- **Promotion and Regulation of Online Gaming Act 2025** (in force 1 May 2026, secondary). It bans online *money*
  games. Interpretation: a free game with in-app purchases and no stake to win is a "social game". Whether social
  games must register is UNVERIFIED (sources conflict).

### L10. Laws against cheat makers

- **South Korea, Game Industry Promotion Act Art. 32(1)(8) and Art. 46(3-2).** Distributing, or making for
  distribution, programs that disturb a game's normal operation is punishable by up to 1 year in prison or
  KRW 10M. An Overwatch cheat maker was jailed under it. https://elaw.klri.re.kr/kor_service/lawViewTitle.do?hseq=49070
- **Other countries:** not verified.

### L11. False positives, and degrading paid service in secret

- **Telling a player "in your system"** when the flag came from chat or statistics is a misleading-claim risk
  (§1.6). GDPR's accuracy duty (Art. 5(1)(d)) and the right to rectification also apply.
- **Defamation:** interpretation, unlikely if only the player sees the notice. The risk grows if party members can
  infer the flag.
- **Secretly degrading paid service** runs into the Epic order, the CPC principles, the Unfair Contract Terms
  Directive Annex and the Digital Content Directive (L4).
- Interpretation: notify promptly, state the real reason category, offer an appeal to a human, and keep paid
  content playable while it runs.

---

## Part P: the platforms

Checked 2026-10-22. "Undated" means the page has no date and we read it on 2026-10-22.

### P1. Steam / Valve

- **Kernel disclosure.**
  - A game that "installs a client side, kernel mode anti-cheat" must fill in the store page's Anti-Cheat section:
    the provider, a "Kernel-level Integration" box, and whether files stay after uninstall.
  - Release review checks it. It is optional for non-kernel or server-only anti-cheat.
  - Source (30 Oct 2024):
    https://steamcommunity.com/groups/steamworks/announcements/detail/4547038620960934857
- **Steamworks anti-cheat guidance** (undated): https://partner.steamgames.com/doc/features/anticheat
  - "Give your customers proper warning that such a system exists."
  - Banned or kicked players "should receive clear messaging in-game".
  - In peer-to-peer games "each user should validate the others".
  - FAQ: "Can I use bans in other games to block users from playing in my game? A: No."
- **Separate servers.** Valve's VAC page describes "secure" and "insecure" servers: "This separates the cheaters
  from the rest of your community while still letting them play the game."
  https://partner.steamgames.com/doc/features/anticheat/vac_integration
- **Game bans** (opt in; calls come from your server with a publisher key):
  - `ReportPlayerCheating` data "is treated as hearsay".
  - `RequestPlayerGameBan`: under a year "is a suspension and not visible on profile"; longer bans are public and
    remove automatic refunds.
  - `RemovePlayerGameBan` undoes false positives.
  - https://partner.steamgames.com/doc/webapi/ICheatReportingService
- **Reading account signals.**
  - `GetPlayerBans`, `GetSteamLevel` and client calls such as `BIsPhoneVerified` exist.
  - The Web API terms (2010) allow retrieving data about a user only "as requested by the end user".
    https://steamcommunity.com/dev/apiterms
  - Interpretation: do not build trust priors from them without counsel.
- **Steam Deck / Proton** (undated): https://partner.steamgames.com/doc/steamdeck/proton
  - "Kernel-space solutions are not currently supported and are not recommended."
  - Easy Anti-Cheat works when enabled per build.
- **VAC detection for new titles:** UNVERIFIED.

### P2. Apple iOS

- **Fingerprinting is banned.** "You may not derive data from a device for the purpose of uniquely identifying it."
  There is no fraud exception. https://developer.apple.com/app-store/user-privacy-and-data-use/
- **Required-reason APIs:** "fingerprinting is not allowed" regardless of tracking permission. Uploads without
  declared reasons have been refused since 1 May 2024.
- **App Attest.** Keys "don't survive app reinstallation, device migration, or restoration of a device from a
  backup". https://developer.apple.com/documentation/devicecheck
- **DeviceCheck.**
  - Two bits per device, plus the month they were last set, stored by Apple.
  - "The state persists across app reinstallation, device transfer between users, and even 'Erase all contents and
    settings.'" Shared by all of one developer's apps (WWDC21 session 10244).
- **What an app can see.**
  - `canOpenURL` works only for declared schemes, up to 50.
  - Guideline 5.1.2(iv) bars collecting installed apps for analytics or advertising.
  - Guideline 2.5.4 limits background services.
  - No way to watch the system.
- **Guideline 1.2** (user content and chat): filtering, reporting with "timely responses", blocking, and published
  contact information. https://developer.apple.com/app-store/review/guidelines/
- **Privacy label.**
  - Third-party SDK data counts.
  - The "App Functionality" purpose covers "prevent fraud, implement security measures".
  - Relevant data types: "Device ID", "Gameplay Content", and "Emails or Text Messages" for in-app messaging.
- **Age ratings:** 4+/9+/13+/16+/18+. The new questions (including "Messaging and Chat" and "User-Generated
  Content") were due 31 Jan 2026.

### P3. Google Play / Android

- **Play Integrity verdicts** (6 Oct 2026): https://developer.android.com/google/play/integrity/verdicts
  - App integrity, device integrity (basic and strong are opt-in) and licensing.
  - Recent device activity, levels 1-4 (opt-in).
  - App access risk (opt-in): apps capturing the screen, controlling the device or drawing overlays.
  - Play Protect verdict.
- **Device recall** (beta, apply by form, 8 Jun 2026):
  https://developer.android.com/google/play/integrity/device-recall
  - 3 bits per device, readable "even after your app is reinstalled or the device is reset", kept 3 years.
  - "You may not use device recall to fingerprint or track individual users or devices."
  - Needs a Play-licensed copy; not on emulators.
- **Device identifiers.** Play bans linking persistent device identifiers (such as IMEI), with no fraud exception.
  Google points to Play Integrity for abuse protection.
- **`QUERY_ALL_PACKAGES`:** permitted only for "device search, antivirus apps, file managers, and browsers".
  Developers are pointed to "alternative security or fraud prevention solutions".
  https://support.google.com/googleplay/android-developer/answer/10158779
- **Data safety:** SDK data counts. "Fraud prevention, security, and compliance" is a listed purpose and "Device or
  other IDs" a listed data type.
- **User-generated content policy:** users accept terms first, then get in-app reporting and blocking, and the
  content gets "robust, effective, and ongoing" moderation.
  https://support.google.com/googleplay/android-developer/answer/9876937
- **Families policy** (if children are in the audience): an online-safety reminder, adult management of social
  features, and no advertising ID.

### P4. Windows and kernel drivers

- **Signing.** New kernel drivers load only when Microsoft signs them through the Dev Portal; an EV certificate is
  needed. Attestation signing is now "For testing purposes only" and is not Windows Certified. Microsoft recommends
  HLK-tested (WHQL) signing.
  https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/driver-signing-offerings
- **Vulnerable-driver blocklist:** on by default since the Windows 11 2022 update, and enforced with HVCI, which is
  "on by-default for most new Windows 11 devices".
- **`GetRuntimeAttestationReport`** (21 Jan 2026): a user-mode call that returns a Secure-Kernel-signed list of
  loaded drivers, "for enforcing anti-cheat policies". It needs TPM 2.0, Secure Boot, VBS, HVCI and IOMMU.
  https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getruntimeattestationreport
- **Riot Vanguard On-Demand** (24 Jun 2026) uses that call; Riot estimates about 35% of players qualify.
  https://www.riotgames.com/en/news/vanguard-on-demand
- **Microsoft's plans after the CrowdStrike outage.**
  - Nov 2024: security tools able to run outside the kernel.
  - Nov 2025: an endpoint-security API in private preview.
  - May 2026: moving third-party kernel drivers to user mode or Microsoft class drivers.
  - No announcement that anti-cheat must leave the kernel was found (UNVERIFIED negative).
- **Security record.**
  - Genshin Impact's `mhyprot2.sys` was abused by ransomware to kill antivirus, with the game not installed. Its
    signature was still valid at the time (Trend Micro, 24 Aug 2022).
    https://www.trendmicro.com/de_de/research/22/h/ransomware-actor-abuses-genshin-impact-anti-cheat-driver-to-kill-antivirus.html
  - Capcom pulled its Street Fighter V driver in 2016.
  - Vanguard's League of Legends launch drew boot-loop reports. Riot said "fewer than 0.03% of players have reported
    issues" (Kotaku, 2024, secondary).

### P5. Off-the-shelf tools and precedents

**Tools:**

| Tool | Platforms | Cost | Note |
|---|---|---|---|
| Easy Anti-Cheat | Windows, Mac, Linux; not Android, iOS or consoles | "The anti-cheat service is free" | Unity via the EOS plugin (https://github.com/EOS-Contrib/eos_plugin_for_unity) |
| Code Stage Anti-Cheat Toolkit | All Unity targets | $80 | Obscured values and speed/time/injection detectors; "No client-side tool makes a game 100% cheat-proof" |
| Talsec freeRASP | Unity 6+, Android, iOS | Free up to 100,000 devices | — |
| Guardsquare DexGuard / iXGuard | Android and iOS | Price not public | Hides IL2CPP metadata |
| BattlEye, Denuvo Anti-Cheat, Promon, AppSealing | — | — | Unity support or pricing UNVERIFIED |

**Precedents:**

| Game | Disclosed? | How a flagged player learns |
|---|---|---|
| Call of Duty | Yes. Its enforcement policy (31 Jul 2026) places flagged accounts "in limited matchmaking lobbies with other accounts in a similar state", with no progression there; "Only permanent bans may be appealed". https://support.activision.com/articles/call-of-duty-security-and-enforcement-policy | Failed TPM/Secure Boot checks get a "Failed Attestation Status" notice. Mitigations such as Hallucinations (decoys only cheaters see) are published. https://www.callofduty.com/blog/2023/06/call-of-duty-ricochet-anti-cheat-season-04-update |
| CS:GO / CS2 Trust Factor | Yes | Not told: "We don't plan to display your Trust Factor." |
| Dota 2 low priority | Yes | Told in game, with the way out (win 5 Single Draft games) |
| Fall Guys "Cheater Island" | Only after it closed | Endless loading screens. It shut down because 40-player matches could not fill (Game Developer, 14 Sep 2020) |

**Court cases:**

- A UK small-claims judge restored an account Activision banned, because Activision presented no evidence of
  cheating (DualShockers, 29 Jan 2025, secondary).
- A reported US shadowban suit was not found (UNVERIFIED).

**Voice moderation:**

- Call of Duty's ToxMod was announced publicly (beta Aug 2023).
- Riot updated its privacy notice and terms before testing voice evaluation (2022).

### P6. Unity Gaming Services

- **Access Control** can deny players' direct writes to Leaderboards and Cloud Save, so Cloud Code writes them.
  Blocked calls return 403. (docs.unity.com: Cloud Code > Server access control; Services > Access control.)
- **Authentication.**
  - Supports Steam (a ticket from `GetAuthTicketForWebApi`), Apple Game Center, Sign in with Apple, Google Play
    Games, Google and Unity Player Accounts, with linking.
  - Anonymous accounts "cannot recover … if they uninstall and then reinstall", and `ClearSessionToken` plus
    `SignInAnonymouslyAsync` makes a new player at will.
- **Matchmaker.**
  - Pool filters use ticket attributes the client sets, so they can be forged.
  - Rules can read `Players.CustomData.ExternalData.CloudSave.*`. That data is in the Protected class by default:
    the player can read it, only a server can write it.
- **Moderation and DSA.**
  - The Moderation SDK takes player reports into a dashboard.
  - Safe Text adds filtering on Vivox text chat. Safe Voice is discontinued (Moderation 1.1.0, Mar 2025).
  - Projects using Unity Authentication are told to integrate its DSA notification API.
- **No server hook on match results.** Triggers cover Authentication, Leaderboards, Cloud Save, Moderation and
  Scheduler only, with no Lobby or Relay events. So Cloud Code, or our own webhook, is the only place to check a
  host-reported result.
- **Multiplay Game Server Hosting** was deprecated on 1 Apr 2026. https://status.unity.com/info_notices/362941

---

## 4. Questions counsel must answer

1. Is isolating matchmaking an Art. 22 "similarly significant" effect? Is locking paid modes one? If so, which
   Art. 22(2) basis applies? The same questions under UK Arts. 22A-22C.
2. Anti-cheat and ePrivacy:
   - Is in-process anti-cheat, plus platform attestation (App Attest, Play Integrity), "strictly necessary" under
     ePrivacy in our main EU markets?
   - Does UK PECR's "fraud" cover cheating?
   - Is a kernel driver ever defensible without consent?
3. DSA:
   - How are display names and any future chat classified?
   - Does Art. 17 reach restrictions for cheating?
   - Is Froglet a micro or small enterprise?
   - Who is our EU legal representative?
4. Can the 3-match delay before the notice stand for cheat-based restrictions, under DSA Art. 17(2) and UK
   Art. 22C? Or must EU and UK players be told when they enter the isolated pool?
5. Minors:
   - What COPPA notice is owed for internal-operations anti-cheat on under-13s?
   - How do we comply with India's s.9(3) for under-18s from May 2027, and does s.17 help?
   - Should minors be excluded from automated sanctions entirely? (The design already does this.)
   - Built 2026-10-25: the birth year is kept and sent to the server, and anyone not surely 18 gets no quit record
     (`RULES.keepConductForMinors = false` in `Tools/UGS/CloudCode/match-report.js`). May quit counting run for
     13-17s (it is a behaviour profile under the UK Children's Code), and is keeping the birth year itself (only the
     year, protected) proportionate for under-13s?
6. Can the Code of Conduct lawfully allow restricting modes for cheating? What refunds are owed, and who is the
   "trader" on Steam and the app stores?
7. What wording and evidence standard should the notice use, so it is not misleading?
8. Is Froglet a covered business under the CCPA yet?
