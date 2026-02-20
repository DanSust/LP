using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;
using ReCastor;

namespace ReCastor
{
    public enum RecipeType
    {
        Standard,
        Extern,
        Special,
        ManDos
    }
    public enum RecipeStep
    {
        ManualAddition,
        ManualAdditionProdSvcQuit,
        ManualWeigh,
        RecalcToLeadingComponent,
        RunProcessStep,
        SDRContainerTransport,
        SDRContInCheck,
        SDRContOutCheck,
        SpezialWeigh,
        TargetContainerCreation,
        TargetContainerIdentification,
        TargetContainerShuttleDos,
        TransmitDDWRecipe,
        TransmitMAInfoBatch,
        TransmitMAInfoStation,
        TransmitPlcRecipe,
        TransmitProcessSteps,
        UnfixRessource,
        UserIdentification
    }

    public class Recipe: BaseNamedEntity
    {
        RecipeType RecipeType { get; set; }
        IList<Tuple<RecipeStep, Func<string, string>>>? Steps { get; set; }
        int BatchSize { get; set; }
    }
}
