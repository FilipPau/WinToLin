using WinToLin.Logic.Enums;
using WinToLin.Migrator.DistroDependent.Build;
using WinToLin.Migrator.DistroDependent.Build.Distros;
using WinToLin.Migrator.DistroDependent.Modification;
using WinToLin.Migrator.DistroDependent.Modification.Distros;
using WinToLin.Migrator.DistroDependent.Preparation;
using WinToLin.Migrator.DistroDependent.Preparation.Distros;

namespace WinToLin.Migrator.DistroDependent.ToolKit;

public static class TransferToolKitFactory
{

    public record ToolKit(
        IPreparationStep PreparationStep,
        IModificationStep ModificationStep,
        IBuildStep BuildStep
    );
    
    private static readonly IReadOnlyDictionary<Distros, Func<ToolKit>> Registry
        = new Dictionary<Distros, Func<ToolKit>>
        {
            [Distros.UBUNTU] = () => new ToolKit(
                new UbuntuPreparationStep(),
                new UbuntuModificationStep(),
                new UbuntuBuildStep()
            ),
            [Distros.FEDORA] = () => new ToolKit(
                new FedoraPreparationStep(),
                new FedoraModificationStep(),
                new FedoraBuildStep()
            ),
        };

    public static ToolKit CreateTransferToolKit(Distros distro)
    {
        if (!Registry.TryGetValue(distro, out var factory))
        {
            throw new NotSupportedException($"Distro not supported: {distro}");
        }

        return factory();
    }
}