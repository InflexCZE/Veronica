# What the model is told

Rules for the context the model works from: skills, wiki, system prompt.

- One skill per feature (block-light, block-gyro, ...): a modder's task combines features, and each skill stays small enough to load when needed. A skill index that grows past about 4K tokens is a sign the skills need better progressive disclosure, not a longer index.
- Game knowledge comes from reads and the wiki, but a model that already knows where to read reads it directly: the context never forces a wiki call over a known read.
- An ambiguous wish from the user is fine to act on when the model picks a reading and confirms it with the user, who can then steer.
