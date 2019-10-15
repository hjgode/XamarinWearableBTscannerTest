using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;

namespace BTApp1
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        bt_barcode bt_class;

        static Button btn_Connect;
        Button btn_Test;
        static bool toggle = false;

        public static TextView txtLog;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            bt_class = new bt_barcode(this);

            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            txtLog = FindViewById<TextView>(Resource.Id.txtLog);
            txtLog.MovementMethod = new Android.Text.Method.ScrollingMovementMethod();

            btn_Connect = FindViewById<Button>(Resource.Id.btn_Connect);
            btn_Connect.Click += Btn_Connect_Click;

            btn_Test = FindViewById<Button>(Resource.Id.btn_Test);
            btn_Test.Click += Btn_Test_Click;
            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;
        }

        private void Btn_Test_Click(object sender, EventArgs e)
        {
            if (bt_class.Connected)
            {
                bt_class.write(util.setDoBeep());

                bt_class.write(util.getDecHeaderSetting());

                bt_class.write(util.setManualTriggerMode());
                bt_class.write(util.setTriggerOnMsg());
                if (toggle)
                {
                    bt_class.write(util.setGoodReadBeepOn());
                    bt_class.write(util.setDecHeaderSetting(true));
                }
                else
                {
                    bt_class.write(util.setGoodReadBeepOff());
                    bt_class.write(util.setDecHeaderSetting(false));
                }
                toggle = !toggle;
            }
        }

        private void Btn_Connect_Click(object sender, EventArgs e)
        {
            if (bt_class.Connected)
            {
                bt_class.disconnect();
                btn_Connect.Text = "Connect";
            }
            if (bt_class.Connect("8680")) // ("8670");
            {
                btn_Connect.Text = "connecting...";
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            switch (id) {
                case Resource.Id.action_settings:
                    return true;
                    
                case Resource.Id.mnu_BeeperOff:
                    {
                        if (bt_class.Connected)
                            bt_class.write(util.setGoodReadBeepOff());
                        return true;
                    }
                case Resource.Id.mnu_BeeperON:
                    {
                        if (bt_class.Connected)
                            bt_class.write(util.setGoodReadBeepOn());
                        return true;
                    }
                case Resource.Id.mnu_DoScan:
                    {
                        if (bt_class.Connected)
                        {
                            bt_class.write(util.setManualTriggerMode());
                            bt_class.write(util.setTriggerOnMsg());
                        }
                        return true;
                    }
                case Resource.Id.mnu_DoBeep:
                        if (bt_class.Connected)
                            bt_class.write(util.setDoBeep());
                        return true;
                case Resource.Id.mnu_HeaderOff:
                        if (bt_class.Connected)
                            bt_class.write(util.setDecHeaderSetting(false));
                        return true;
                case Resource.Id.mnu_HeaderOn:
                        if (bt_class.Connected)
                            bt_class.write(util.setDecHeaderSetting(true));
                        return true;
                case Resource.Id.mnu_ManualTrigger:
                        if (bt_class.Connected)
                            bt_class.write(util.setManualTriggerMode());
                        return true;
                case Resource.Id.mnu_AllDisable:
                        if (bt_class.Connected)
                            bt_class.write(util.SetAllSymbologiesOnOff(false));
                    return true;
                case Resource.Id.mnu_AllEnable:
                        if (bt_class.Connected)
                        bt_class.write(util.SetAllSymbologiesOnOff(true));
                    return true;

            }
            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            View view = (View) sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        /// <summary>
        /// This can be called before the connected ReadThread is running
        /// </summary>
        /// <param name="sMsg">Message to show</param>
        public static void onStatusChanged(string sMsg)
        {
            if(txtLog!=null)
                txtLog.Text += sMsg + "\r\n";
        }

        public static void onConnectStatusChanged(bt_barcode.eStatus iState)
        {
            switch (iState)
            {
                case bt_barcode.eStatus.STATE_CONNECTED:
                    btn_Connect.Text = "Disconnect";
                    break;
                case bt_barcode.eStatus.STATE_NONE:
                    btn_Connect.Text = "Connect";
                    break;
            }
        }
	}
}

