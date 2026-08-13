# Manager Lifecycle sample

Open `ManagerLifecycle.unity` and enter Play Mode. The Game view displays the Manager state, shared values, and the independent values of two `MoteScriptBehaviour` instances.

The sample demonstrates:

- the Manager starting before its registered behaviours;
- `Awake` running once and `Enable` running after each activation;
- `Disable` running on deactivation and `Destroy` running once on teardown;
- a yielding Manager `Start` delaying all behaviour `Start` calls;
- independent Context values on each GameObject;
- Inspector-configured initial variables applied before each script's `Awake`;
- values shared through `shared`;
- a standard Math library registered through `library`;
- deterministic behaviour execution priority (`Early` then `Late`);
- yielding `Start` and `Update` functions; and
- switching between live targets in the MoteScript Debugger.

The shared `order` value should become `12` after each completed update: the priority-0 behaviour appends `1`, then the priority-100 behaviour appends `2`.

Each `MoteScriptBehaviour` owns its `identifier` and `step` values in the **Initial Variables** Inspector list. This allows the same `BehaviourLifecycle.mote` asset to be reused with different per-GameObject parameters. Number values are converted to the numeric type selected by the Manager; String and Null values are kept distinct.

Open the debugger from **Window > MoteScript > Debugger** while the scene is running. Select the Manager, Early Behaviour, or Late Behaviour from the Target field to inspect its current execution.
