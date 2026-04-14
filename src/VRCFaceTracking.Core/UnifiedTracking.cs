using VRCFaceTracking.Core.Params;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Types;

namespace VRCFaceTracking
{
    /// <summary>
    /// Class that contains all relevant tracking data.
    /// </summary>
    public class UnifiedTracking
    {
        /// <summary>
        /// Eye image data sent from the loaded eye module.
        /// </summary>
        public static Image EyeImageData = new Image();

        /// <summary>
        /// Lip / Expression image data sent from the loaded expressions module.
        /// </summary>
        public static Image LipImageData = new Image();

        /// <summary>
        /// Latest Expression Data accessible and sent by all VRCFaceTracking modules.
        /// </summary>
        public static UnifiedTrackingData Data = new();

        /// <summary>
        /// Container of all features and functions that mutates the incoming expression data into output data suitable for driving Unified Expressions.
        /// </summary>
        public static UnifiedTrackingMutator Mutator = null!;

        /// <summary>
        /// Version 2 (Unified Expressions) of all accessible output parameters.
        /// </summary>
        public static readonly Parameter[] AllParameters_v2 = UnifiedExpressionsParameters.ExpressionParameters;

        /// <summary>
        /// Head tracking parameters.
        /// </summary>
        public static readonly Parameter[] HeadParameters = UnifiedHeadParameters.HeadParameters;

        /// <summary>
        /// The collection of EVERY possible output parameter.
        /// </summary>
        public static readonly Parameter[] AllParameters = AllParameters_v2.Concat(HeadParameters).ToArray();

        /// <summary>
        /// Central update action for all expression data to subscribe to.
        /// </summary>
        public static Action<UnifiedTrackingData> OnUnifiedDataUpdated = OnUnifiedDataUpdated + (_ => { }) ?? (_ => {});

        /// <summary>
        /// Central update function that updates all output parameter data and pushes the latest expressions from VRCFaceTracking modules into the internal expressions buffer.
        /// </summary>
        public static async Task UpdateData(CancellationToken ct) =>
            OnUnifiedDataUpdated.Invoke(await Task.Run(() => Mutator?.MutateData(Data) ?? Data, ct));
    }
}
