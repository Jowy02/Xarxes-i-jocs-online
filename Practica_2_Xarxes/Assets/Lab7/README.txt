LAB 7 - WORLD STATE REPLICATION
XJO - Xarxes per a Jocs Online

WHAT IS IN THIS HANDOUT
  Scenes/S_ReplicationTest                      a test scene. No game, no lobby, works on its own
  Scripts/NetObject.cs                          every object writes and reads itself (slide 9). Nothing to do
  Scripts/NetPlayer.cs                          a NetObject with health, the model for your own (slide 9). Nothing to do
  Scripts/Lab7LinkingContext.cs                 the finished Lab 6 linking context (slide 15 of Lab 6). Nothing to do
  Scripts/ReplicationManagerServer.cs           marks and writes the packet (slides 10, 11, 15). TODO 1 and 2
  Scripts/ReplicationManagerClient.cs           reads the packet (slide 13). TODO 3 to 5
  Scripts/ReplicationTest.cs                    checks your TODOs, then the live view
  Scripts/ReplicationManagerServerSolution.cs   the finished server manager
  Scripts/ReplicationManagerClientSolution.cs   the finished client manager
  Everything is in the namespace Lab7, so it does not clash with your own classes.

IN CLASS, PART 1 (slide 19)
  Open S_ReplicationTest and press Play. Every check says PASS, FAIL or SKIPPED.
  Do TODO 1 and 2 in ReplicationManagerServer.cs, then TODO 3, 4 and 5 in
  ReplicationManagerClient.cs, until all 15 checks say PASS.
  Stuck? Disable the GameObject ReplicationTest, enable ReplicationTestSolution,
  press Play and compare. The code is in the two *Solution.cs files.
  When all checks pass, the live view starts. The server world on the left, the client copy on the
  right, a simulated network in between. Turn loss to 20 % and jitter up. Objects may lag, but no
  ghost stays and nothing is missing for more than about a second.

IN CLASS, PART 2 (slide 20)
  Add NetObject (or a subclass) to every networked prefab, and fill the prefab list on both sides.
  On the server, call MarkCreate, MarkUpdate and MarkDestroy, and send WritePacket every tick.
  On the client, call ReadPacket from your OnPacketReceived.

DELIVERABLE 4 (10 %)
  World state replication, presented in this session (slides 21 to 28). It starts from this code.
