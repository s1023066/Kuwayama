using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    public partial class TeamWindow : Form
    {
        public TeamWindow()
        {
            InitializeComponent();
            string[] str = new string[]{"1123043 石田悠：プロジェクトリーダー, メインコーダー",
                             "1123109 古谷政人：プログラムサポート",
                             "1123033 小島健太郎：Wiki担当",
                             "1123152 野崎郁美：ウィンドウデザイン",
                             "1023066 桑山英明：チーム記録担当, カメラ",
                             "神奈川工科大学 情報学部 情報メディア学科"};
            
            Namelabel.Text = str[0] + "\n" + str[1] + "\n" + str[2] + "\n" + str[3] + "\n" + str[4] + "\n\n" + str[5];
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
