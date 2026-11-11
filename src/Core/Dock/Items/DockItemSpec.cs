namespace Koy.Core.Dock.Items;

/// <summary>Doğrulanmış dock öğesi. Yalnız geçerli türler, dolu kimlikler ve tekilleştirilmiş kayıtlar bu tipe girer.</summary>
public sealed record DockItemSpec(DockItemKind Kind, string Id, string Command);
