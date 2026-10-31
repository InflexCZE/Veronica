# present_graph: node-limit refusals and graphs standing in for show

Seen in eval runs with the 27B (one run each, 2026-10-31); open for the owner.

## Node limit

The refusal names the count and the limit ("21 nodes; at most 20 fit on the board"). In one build run the model planned a removal in its reasoning and sent the same 21 nodes three more times (about 5.8K of context) before it passed with 20.

Options:
- The refusal says how many nodes to drop ("drop 1 node"), counted from the call it received. Recommended: the model miscounted its own list, and the tool knows the count.
- Raise the limit, if the board can show more.
- Leave it until it recurs.

## Graph as the result

In a build task (an override of the ion thrusters' flame and thrust) the model drew a graph as its show step and called `show` on the unchanged vanilla flame effect, not on one of its overrides. A skill's Show step could say to show a changed item; one run is too few to word it on.
