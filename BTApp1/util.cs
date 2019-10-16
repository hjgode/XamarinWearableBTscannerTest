﻿using System;
using System.Text;

namespace BTApp1
{
    public class util
    {
        public static string toHex(string sIN)
        {
            if (sIN == null)
                return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            char[] cArr = sIN.ToCharArray();
            foreach (char c in cArr)
            {
                if (c < ' ')
                {
                    eASCII_codes asciiName = (eASCII_codes)(byte)c;
                    sb.Append("<0x" + ((byte)c).ToString("x02") + "/"+ asciiName + ">");
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /*  Without MsgHeader:
         *  10110<d><0><0><0
         *  
         *  With MSGHDR:
         *  <16>?$<0><0><0><d>MSGGET0022IC1<1d>1012345678<1d>2187654321<d><0><0>
         *  <1d>1012345678<1d>2187654321<d>
         *  <16>?
         *      $<0><0><0>
         *          <d>
         *              MSGGET
         *                  0022    len as string
         *                      IC1<1d>1012345678<1d>2187654321<d><0><0>
         *  [SYN]?$[NUL][NUL][NUL][CR]MSGGET0022
         *  hex 22 => dec 34
         *  
         *  7 character SYN FE header, 
              where byte 0 is a <SYN> (0x16) character, 
              byte 1 is a 0xFE character, 
              byte 6 is a <CR> (0x0D) character, and bytes 2 to 5 are the little 
              endian length of the MSGGET command and data following this header.
            6 character MSGGET command.
            4 character decimal length of the barcode data. For example, 0015 indicates a bar code data length of 15 decimal.
            1 character Honeywell Code ID
            1 character AIM ID
            1 character AIM modifier
            <GS> (0x1D) character to mark the end of the command / parameters and the beginning of data.
            Actual bar code data.
        */
        public static BarcodeData getBarcodeData(byte[] bData)
        {
            BarcodeData bcData = new BarcodeData("   ");
            if (bData[0] == 0x16 && bData[1] == 0xFE && bData[6] == 0x0d)
            {
                byte[] bLen = new byte[4];
                Array.ConstrainedCopy(bData, 2, bLen, 0, 4);
                UInt32 uL = BitConverter.ToUInt32(bLen);
                byte[] bDataMSG = new byte[uL];
                Array.ConstrainedCopy(bData, 7, bDataMSG, 0, (int)uL);
                //=> MSGGET0022IC1<1d>1012345678<1d>2187654321<d><0><0>
                string sMSG = Encoding.UTF8.GetString(bDataMSG);
                //now we get the message length
                int iMsgLen = int.Parse(sMSG.Substring(6, 4));// look at MSGGET0022...
                bcData = new BarcodeData(sMSG.Substring(10, 3));
                bcData.sData = sMSG.Substring(13, iMsgLen);

            }
            return bcData;
        }

        public static string sIsACK(string sIN)
        {
            int iPosAck = sIN.IndexOf("\x06");
            if (iPosAck >= 0)
            {
                return sIN.Substring(0, iPosAck);
            }
            return "";
        }

        public static string sIsDataOnly(string sIN)
        {
            int iPosCR = sIN.IndexOf("\x0D");
            if (iPosCR >= 0)
            {
                return sIN.Substring(0, iPosCR);
            }
            return "";

        }
        public class BarcodeData
        {
            public string honID { get; set; }
            public string AimID { get; set; }
            public string AimMod { get; set; }
            public string sData { get; set; }
            public BarcodeData(string sIDs)
            {
                honID = sIDs.Substring(0, 1);
                AimID = sIDs.Substring(1, 1);
                AimMod = sIDs.Substring(2, 1);
            }
            public override string ToString()
            {
                return sData;
            }
        }

        public static byte[] setTriggerOnMsg()
        {
            // SYN T CR
            return Encoding.UTF8.GetBytes("\x16T\x0d");
        }
        public static byte[] setTriggerOffMsg()
        {
            // SYN U CR
            return Encoding.UTF8.GetBytes("\x16U\x0d");
        }
        public static byte[] setManualTriggerMode()
        {
            string s = "PAPHHF.";
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setTriggerClickOn()
        {
            string s = "BEPTRG1.";
            return Encoding.UTF8.GetBytes(s);
        }
        public static byte[] setTriggerClickOff()
        {
            string s = "BEPTRG0.";
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setGoodReadBeepOff()
        {
            string s = SynMenuCommandHeader + "BEPBEP0" + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setGoodReadBeepOn()
        {
            string s = SynMenuCommandHeader + "BEPBEP1" + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] getDecHeaderSetting()
        {
            string s = SynMenuCommandHeader + "DECHDR?" + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setDecHeaderSetting(bool bOnOff)
        {
            string s = SynMenuCommandHeader + "DECHDR" + (bOnOff ? "1" : "0") + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] setDoBeep()
        {
            string s = SynMenuCommandHeader + "BEPEXE1" + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        public static byte[] SetAllSymbologiesOnOff(bool bOnOff)
        {
            string s = SynMenuCommandHeader + "ALLENA" + (bOnOff ? "1" : "0") + SynMenuCommandSuffixFlash;
            return Encoding.UTF8.GetBytes(s);
        }

        static string deviceName = ""; //or ":*:" or BT friendly name, ie ":8680i wearable:
        public static string SynMenuCommandHeader = "\x16M\x0d" + deviceName; //:*: = name of device
        public static string SynYCommandHeader = "\x16Y\x0d" + deviceName; //:*: = name of device
        public static string SynMenuCommandSuffixFlash = ".";
        public static string SynMenuCommandSuffixRAM = "!";
        public enum eASCII_codes
        {
            NUL = 0x00, //"null" character
            SOH = 0x01, //start of header
            STX = 0x02, //start of text
            ETX = 0x03, //end of text
            EOT = 0x04, //end of transmission
            ENQ = 0x05, //enquiry
            ACK = 0x06, //acknowledgment
            BEL = 0x07, //bell
            BS = 0x08, //backspace
            HT = 0x09, //horizontal tab
            LF = 0x0A, //line feed
            VT = 0x0B, //vertical tab
            FF = 0x0C, //form feed
            CR = 0x0D, //carriage return
            SO = 0x0E, //shift out
            SI = 0x0F, //shift in
            DLE = 0x10, //data link escape
            DC1 = 0x11, //device control 1 (XON)
            DC2 = 0x12, //device control 2
            DC3 = 0x13, //device control 3 (XOFF)
            DC4 = 0x14, //device control 4
            NAK = 0x15, //negative acknowledgement
            SYN = 0x16, //synchronous idle
            ETB = 0x17, //end of transmission block
            CAN = 0x18, //cancel
            EM = 0x19, //end of medium
            SUB = 0x1A, //substitute
            ESC = 0x1B, //escape
            FS = 0x1C, //file separator
            GS = 0x1D, //group separator
            RS = 0x1E, //request to send/record separator
            US = 0x1F, //unit separator
        }
    }
}