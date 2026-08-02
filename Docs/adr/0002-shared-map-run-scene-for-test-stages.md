# Use one MapRun scene for all Test Stages

All three Test Stages load the same `MapRun` scene and pass the `MapRunConfigSO` selected at the Stage Entrance from the Basement. This keeps room authoring, NavMesh setup, party spawning, and runtime controllers shared; duplicating the scene per stage was rejected because its serialized setup would drift as the map system evolves.
