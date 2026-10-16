using Content.Shared._Utopia.Genetics.Components;

namespace Content.Shared._Utopia.Genetics.Systems;

public abstract partial class SharedGeneticAnalyzerSystem : EntitySystem
{
    public void ClearScan(EntityUid uid, GeneticAnalyzerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.PatientName = null;
        comp.PatientInstability = 0;
        comp.Mutations.Clear();

        Dirty(uid, comp);
    }

    public void SetScanResults(EntityUid uid, string patientName, int instability, List<MutationEntry> mutations, GeneticAnalyzerComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.PatientName = patientName;
        comp.PatientInstability = instability;
        comp.Mutations = mutations;

        Dirty(uid, comp);
    }
}
