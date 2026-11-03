# Scheduler independent static review

Decision: **accepted-bounded-static**. All 14 claims are accepted only within the conditions below. No product implementation, native execution, emulation or runtime timing check occurred.

The source archive identity and semantic review are separate: 148 sealed artifacts and 23 external whole-file pins pass; the BIN is rechecked only through bounded bytes and PE mapping. Its whole SHA remains inherited. The 39 original proof ranges are decoded independently, with two connecting ranges added inside already verified chunks.

## P01 — accepted conditional source

A46DDA writes frame index0, A4358C preincrements it; with count4 the first binding is index1. A46E65..A46E7E passes D1+1 to A231C0 and stores its returned sample+D1+1 at actor+0xBC. +0xBC is decimal188, not +0x188.

Requested15 must survive current-action8 and special override/early routes; valid stable four-frame metadata. Frame1 binding happens before the initial clock sample N0.

## P02 — accepted conditional source

A437DB resets index0, A437E4 calls A3EF40, stores its returned duration and A43822 returns false. Continuing branch A43894 binds the incremented frame and returns true. Thus normal count4 sequence is 1,2,3,terminal0. Frame selection uses the current frame record; A3FD8F calls A407D0.

The metadata variant branch before reset may change variant. No universal no-loop or profile eligibility claim; native screen presentation is unmeasured.

## P03 — accepted conditional source

A4B4BE calls A43560. False takes the terminal path; +0xD8 nonzero with action!=8 sets passed duration to1. A4B594..A4B5C8 passes that duration to vtable+0x48 and bypasses continuing recurrence. Supplemental A4B4F3..A4B594 contains separate action8/66..69 handling, not an ordinary action15 duration rewrite.

D0 means the newly bound frame0 duration only when +0xD8=0 and the stated ordinary branch applies.

## P04 — accepted conditional source

A44500 nonempty queue schedules A4BF00 with the passed duration using fresh A231C0 clock, with no added +1. Empty ordinary branch sets delay0x248 at A446C1 unless an existing idle/action9 case chooses a frame duration; +34!=0 instead selects action71. A446E4 queues weapon+3 via +64 and A446FF onward schedules its consumer.

584 requires ordinary weapon, present weapon+3 metadata !=15, +34=0, self guards allowing scheduling, derived queue dispatch and successful allocation. It is not a request lock, unconditional immediate action0 reset, or measured total latency.

## P05 — accepted table identities; conditional binding

Three original vtables were decoded as uint32 data. +64 is base51E860 no-op versus derivedA3C4F0. +48 is base51E8E0 wrapper toA44500 versus directA44500. +4C base51E920 and derived521E20 reachA467A0; the derived extra wrapper path is action8-specific.

Six inherited constructor/destructor reference chunks match BIN bytes; table availability does not prove every runtime actor has a derived binding.

## P06 — accepted conditional source

A3C551 allocates8 bytes, A3C56D writes226 and A3C579 writes the action. A3C589 appends viaA3C100: buffer[count]=command, count++. Actor array is at+0x19C, buffer at+0x1A4. Duplicate suppression requires count>3, previous opcode226, matching action and requested action2.

Allocation failure appends nothing. Action15 is not suppressed by that action2 rule. This is an internal command record, not proof of a network opcode identity.

## P07 — accepted conditional source

A44B60 reads index0 then removes it by92AD40, which shifts four-byte pointers and decrements count. A4BF00 pops before dispatch; opcode226 selects byte index8 and targetA4BFB9 in the verified switch data. A4C000..A4C012 passes command.action through vtable+0x4C; A4C138 frees the popped command.

FIFO holds for pending commands subject to separately proven deletion/preemption paths; switch tables are data, not executable linear disassembly.

## P08 — accepted restricted preemption

A3EBF0 with nonempty queue and frame0 removes/frees an idle head and cancels A4BF00 for that actor before returning false. Predicate is (opcode226 AND head.action=weapon+3) OR (head.action=9 AND A43170), with no opcode guard on the second term. Empty queue has a separate current idle/action9 branch cancelling A4B060. A23AC0 requires both callback and context match and retries the same index after erasure.

Nonzero frame remains busy in the queued branch; ordinary queued action15 is not an idle head when weapon+3!=15. This does not establish universal incoming-action cancellation.

## P09 — accepted conditional clear and queue routing

92CFFE checksA3EBF0; busy incoming8 enters the clear loop. Nonnull opcode!=186 reaches free/null/erase at92D250..92D298 and resets scan index=-1. Pending226/action15 can be erased. Afterwards currentaction8 skips append; otherwise +64 receives the incoming action. Nonbusy path calls +4C.

Null entries take92D056..92D068 erase then increment, so a shifted successor can be skipped. Thus all-clear requires an appropriate nonnull queue invariant, not established universally. A44BB0 is a separate matching226/action erase loop with increment after erase, no explicit free or scheduler cancellation; adjacent matches may be skipped.

## P10 — accepted clock expression only

A228B0 branches on configured clock state. QPC is multiplied by1000 through103D750 and divided by the configured64-bit value through103D5E0, returning EAX; alternate branch callsGetTickCount. Both target imports are literal named PE imports independently verified.

Clock selection/frequency initialization and actual values remain untraced. Low32-bit scheduler arithmetic is not a wrap-safe elapsed-time proof.

## P11 — accepted static insertion semantics

A231C0 samplesA228B0, adds caller delay, inserts and returns that sample. A23260 instead adds caller-supplied base to delay without resampling. Both scan deadlines backward with unsigned ja: later deadlines are skipped, equal deadlines retain insertion after existing equals. A22830 shifts16-byte records.

Static insertion order only; no native execution, observed cadence, wrap safety or thread-interleaving guarantee.

## P12 — accepted source plus bounded algebra

A4B5D8 computes lateness=N-B from actor+0xBC; A4B5E4 computesD-lateness. 4E7F50 uses signed jge to select max(1,D-lateness); A4B616 adds1. A23260 receives that delay and sampled N. Independently A4B62E updates B toB+D+1.

Without wrap/signed overflow: due=N+max(1,D-(N-B))+1=max(N+2,B+D+1). Baseline is ideal, not replaced by the clamped due. It is not a per-actor catch-up loop.

## P13 — accepted source

A23372 samples clock once. Loop atA2337A compares unsigned sampledNow>=headDeadline atA23394, copies and removes index0 before invoking callback(context), then jumps back without another sample.

A callback may itself sample a later time; that does not refresh this pump sample. No pump caller cadence, render cadence or actual due-to-screen latency established.

## P14 — accepted conditional duration formula

A407D0 distinguishes self encrypted-global status/gfx from nonself actor fields. Fixed-duration path reads the decoded frame duration field; variable-FPS paths are separate. Nonself +0x19C thresholds divide by16/8/4/2 at counts>=16/8/4/2 respectively, truncating toward zero, before later status/speed transforms. Speed1 applies(3*D)>>2 and Speed2 doubles under the selected branch.

Self and remote are not interchangeable. Neutral means applicable secondary/Brave,+0x220,+0x223,A41C30 and other modifiers absent, no variable FPS, and remote count<=1 at every duration sample. Brave0 alone is insufficient. The special list literal0x15 is decimal21, not pickup15. Base41/166 is an inherited example input, not newly proven upstream tick conversion.

## Arithmetic and adoption boundary

The independent arithmetic starts with inherited fixed durations [41,41,166,166]. Speed1 is applied after that conversion, yielding [30,30,124,124]; Speed2 yields [82,82,332,332]. These are example inputs and transformations, not observations of native clock or screen time.

| Speed | Frame1/2/3/terminal0 relative nodes | Queued consumer | Empty ordinary consumer |
|---|---|---|---|
| 0 | [0, 42, 209, 376] | 417 | 960 |
| 1 | [0, 31, 156, 281] | 311 | 865 |
| 2 | [0, 83, 416, 749] | 831 | 1333 |

N0 is the initial scheduler sample; binding frame1 precedes it. Terminal scheduling samples fresh Nt, so its symbolic deadline is Nt+passedD0 or Nt+584. Zero overhead is only the table convention. For B=42,D=166: N=100 gives newB=209,due=209; N=300 gives newB=209,due=302. No wrap/overflow is assumed.

The proposed authoritative action15 start, ownership-token cancellation, finite presentation instance and Speed0/1 capability guard are engine design proposals. Static native idle preemption and action8 handling inform them but do not prove all engine cancellation rules. The proposal remains unimplemented and its neutral capability guard is not qualified by this review. Existing900ms/3000ms policies are not native-derived timing.

No unsealed eligibility research was read or adopted. Clock initialization, pump caller cadence, runtime table/variant/status eligibility, full cancellation universe, renderer presentation, input origin and complete utility latency remain open. Speed2 is arithmetic only; Slow qualification remains false.
