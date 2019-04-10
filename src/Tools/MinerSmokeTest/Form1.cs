﻿using NiceHashMiner;
using NiceHashMiner.Algorithms;
using NiceHashMiner.Configs;
using NiceHashMiner.Devices;
using NiceHashMiner.Miners.Grouping;
using NiceHashMinerLegacy.Common.Enums;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MinerSmokeTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.dgv_algo.Rows.Clear();
            this.dgv_devices.Rows.Clear();

            dgv_devices.CellContentClick += dgv_devices_CellContentClick;
            dgv_algo.CellContentClick += dgv_algo_CellContentClick;

            this.Shown += new EventHandler(this.FormShown);
        }

        public static object[] GetDeviceRowData(ComputeDevice d)
        {
            object[] rowData = { d.Enabled, d.GetFullName() };
            return rowData;
        }

        public static object[] GetAlgorithmRowData(Algorithm a)
        {
            object[] rowData = { a.Enabled, a.AlgorithmName, a.MinerBaseTypeName };
            return rowData;
        }

        private async void FormShown(object sender, EventArgs e)
        {
            ConfigManager.GeneralConfig.Use3rdPartyMiners = Use3rdPartyMiners.YES;
            await ComputeDeviceManager.QueryDevicesAsync(null, false);
            var devices = AvailableDevices.Devices;

            foreach(var device in devices)
            {
                dgv_devices.Rows.Add(GetDeviceRowData(device));

                var newRow = dgv_devices.Rows[dgv_devices.Rows.Count - 1];
                newRow.Tag = device;
            }
            // disable/enable all by default 
            foreach (var device in devices)
            {
                foreach (var algo in device.GetAlgorithmSettings()) {
                    algo.Enabled = true;
                }
            }
        }


        private void dgv_devices_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dgv_algo.Rows.Clear();
            if (!(e.RowIndex >= 0)) return;

            var senderGrid = (DataGridView)sender;
            var row = senderGrid.Rows[e.RowIndex];

            ComputeDevice device;
            if (row.Tag is ComputeDevice dev) {
                device = dev;
            } else
            {
                // TAG is not device type
                return;
            }

            var cellItem = row.Cells[e.ColumnIndex];
            if (cellItem is DataGridViewCheckBoxCell checkbox)
            {
                var deviceEnabled = checkbox.Value != null && (bool)checkbox.Value;
                checkbox.Value = !deviceEnabled;
                device.SetEnabled(!deviceEnabled);
            }
            var algorithms = device.GetAlgorithmSettings();
            foreach (var algo in algorithms)
            {
                dgv_algo.Rows.Add(GetAlgorithmRowData(algo));

                var newRow = dgv_algo.Rows[dgv_algo.Rows.Count - 1];
                newRow.Tag = algo;
            }
        }

        private void dgv_algo_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (!(e.RowIndex >= 0)) return;

            var senderGrid = (DataGridView)sender;
            var row = senderGrid.Rows[e.RowIndex];

            Algorithm algo;
            if (row.Tag is Algorithm a)
            {
                algo = a;
            }
            else
            {
                // TAG is not algo type
                return;
            }

            var cellItem = row.Cells[e.ColumnIndex];
            if (cellItem is DataGridViewCheckBoxCell checkbox)
            {
                var deviceEnabled = checkbox.Value != null && (bool)checkbox.Value;
                checkbox.Value = !deviceEnabled;
                algo.Enabled = !deviceEnabled;
            }
        }

        private async void btn_startTest_Click(object sender, EventArgs e)
        {
            //var miningTime = TimeSpan.FromSeconds(30);
            int minM, minS, minMS;
            int.TryParse(tbx_minTimeM.Text, out minM);
            int.TryParse(tbx_minTimeS.Text, out minS);
            int.TryParse(tbx_minTimeMS.Text, out minMS);
            int minTime = (60 * minM * 1000) + (1000 * minS) + minMS;
            var miningTime = TimeSpan.FromMilliseconds(minTime);

            int delayM, delayS, delayMS;
            int.TryParse(tbx_stopDelayM.Text, out delayM);
            int.TryParse(tbx_stopDelayS.Text, out delayS);
            int.TryParse(tbx_stopDelayMS.Text, out delayMS);
            int delayTime = (60 * delayM * 1000) + (1000 * delayS) + delayMS;
            var stopDelayTime = TimeSpan.FromMilliseconds(delayTime); //TimeSpan.FromSeconds(1);
            var enabledDevs = AvailableDevices.Devices.Where(dev => dev.Enabled);

            var testSteps = enabledDevs.Select(dev => dev.GetAlgorithmSettings().Where(algo => algo.Enabled).Count()).Sum();
            var step = 0;
            foreach (var device in enabledDevs)
            {
                var enabledAlgorithms = device.GetAlgorithmSettings().Where(algo => algo.Enabled);
                foreach (var algorithm in enabledAlgorithms)
                {
                    step++;
                    try {
                        var miner = NiceHashMiner.Miners.MinerFactory.CreateMiner(device.DeviceType, algorithm);

                        var pair = new List<MiningPair> { new MiningPair(device, algorithm) };
                        var miningSetup = new MiningSetup(pair);

                        miner.InitMiningSetup(miningSetup);
                        var url = StratumHelpers.GetLocationUrl(algorithm.NiceHashID, "eu", miner.ConectionType);

                        tbx_info.Text += $"TESTING: {Environment.NewLine}";
                        tbx_info.Text += $"Device: {device.GetFullName()} {Environment.NewLine}";
                        tbx_info.Text += $"Miner path: {algorithm.MinerBinaryPath}" + Environment.NewLine;
                        tbx_info.Text += $"Miner base: {algorithm.MinerBaseTypeName}" + Environment.NewLine;
                        tbx_info.Text += $"Algorithm: {algorithm.AlgorithmName}" + Environment.NewLine;

                        label1.Text = $"{step} / {testSteps}";

                        tbx_info.Text += $"Starting miner running for {miningTime.ToString()}" + Environment.NewLine;
                        miner.Start(url, Globals.DemoUser, "test");

                        await Task.Delay(miningTime);
                        tbx_info.Text += $"Stopping" + Environment.NewLine;
                        var checkedButton = gb_stopMiningBy.Controls.OfType<RadioButton>()
                                      .FirstOrDefault(r => r.Checked);
                        if(checkedButton.Name == "rb_endMining")
                        {
                            miner.End();
                        } else
                        {
                            miner.Stop();
                        }

                        tbx_info.Text += $"Delay after stop {stopDelayTime.ToString()}" + Environment.NewLine;
                        await Task.Delay(stopDelayTime);
                        tbx_info.Text += $"DONE" + Environment.NewLine + Environment.NewLine;
                    } catch (Exception ex)
                    {
                        tbx_info.Text += $"Exception {ex}" + Environment.NewLine;
                    }
                } 
            }
        }
    }
}