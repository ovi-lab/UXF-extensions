
using System;
using System.Collections.Generic;

namespace ubco.ovilab.uxf.extensions
{
    public interface IExperimentCalibration<out T>
    {
        /// <summary>
        /// All calibration methods configured through
        /// <see cref="AddCalibrationMethod"/> should call this method
        /// to transfer control back to the
        /// <see cref="ExperimentManager"/>.
        /// </summary>
        /// <param name="calibrationParameters">This value will be
        /// recorded in the settings of the block
        /// (<see cref="ConfigureBlockBase"/>) </param>
        public void CalibrationComplete(Dictionary<string, object> calibrationParameters);
        /// <summary>
        /// Add a calibration method.  If the value of a given blocks
        /// <see cref="BlockData.calibrationName"/> matches this name,
        /// the corresponding action will be called. The action should
        /// call <see cref="CalibrationComplete"/> to allow the
        /// experiment to proceed.
        /// </summary>
        /// <param name="name">Name of the calibration method.</param>
        /// <param name="action">Callback to initiate a calibration
        /// with a <see cref="BlockData"/> as parameter, which
        /// represents the current config of the block. </param>
        public void AddCalibrationMethod(String name, Action<T> action);
    }
}
