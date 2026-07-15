using Cerebrum.Core.Components;

namespace Cerebrum.Core.Discovery;

public sealed record ResolvedComponent(ComponentDefinition Definition, string Path, string Source);
