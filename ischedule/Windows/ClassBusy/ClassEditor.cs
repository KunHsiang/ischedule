﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Sunset.Data;

namespace ischedule
{
    /// <summary>
    /// 單一時間表（含時間表分段）編輯器
    /// 需要補上資料行驗證訊息。
    /// </summary>
    public partial class ClassEditor : UserControl, IContentEditor<ClassPackage>
    {
        private ClassPackage mClassPackage;
        private TimeTableBusyEditor mTimeTableBusyEditor = null;
        private List<SchPeriod> mPeriods = new List<SchPeriod>();
        private string mEditorName = string.Empty;
        private bool mIsDirty = false;
        private Scheduler schLocal = Scheduler.Instance;
        private int mSelectedRowIndex;

        /// <summary>
        /// 無參數建構式
        /// </summary>
        public ClassEditor()
        {
            InitializeComponent();

            ReiEver();
        }

        #region IContentEditor<List<TimeTableSec>> 成員
        private void ReiEver()
        {
            menuBusy.Click += (sender, e) =>
            {
                mTimeTableBusyEditor.SetBusy();
                IsDirty = true;
            };

            menuBusyDesc.Click += (sender, e) =>
            {
                mTimeTableBusyEditor.SetBusyDes();
                IsDirty = true;
            };

            menuFree.Click += (sender, e) =>
            {
                mTimeTableBusyEditor.SetFree();
                IsDirty = true;
            };

            grdTimeTableBusyEditor.CellDoubleClick += (sender, e) =>
            {
                foreach (DataGridViewCell Cell in grdTimeTableBusyEditor.SelectedCells)
                {
                    if (Cell.ColumnIndex != 0 && Cell.Style.BackColor != Color.LightGray)
                    {
                        Cell.Value = ("" + Cell.Value).Equals(string.Empty) ? mTimeTableBusyEditor.DefaultBusyMessage : string.Empty;
                        grdTimeTableBusyEditor.EndEdit();
                        IsDirty = true;
                    }
                }
            };
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Prepare()
        {
            mTimeTableBusyEditor = new TimeTableBusyEditor(grdTimeTableBusyEditor);

            TimeTables TimeTables = schLocal.TimeTables;
            cmbTimeTables.Items.Clear();

            List<TimeTable> vTimeTables = new List<TimeTable>();

            foreach (TimeTable vTimeTable in TimeTables)
                if (!vTimeTable.Name.Equals("未指定"))
                    vTimeTables.Add(vTimeTable);

           cmbTimeTables.Items.AddRange(vTimeTables.OrderBy(x => x.Name).ToArray());
            
           if (cmbTimeTables.Items.Count > 0)
                cmbTimeTables.SelectedIndex = 0;
        }

        /// <summary>
        /// 是否被修改
        /// </summary>
        public bool IsDirty
        {
            get
            {
                return mIsDirty;
            }
            set
            {
                mIsDirty = value;

                lblName.Text = mIsDirty ? mEditorName + "<font color='red'>（已修改）</font>" : mEditorName;
            }
        }

        /// <summary>
        /// 取得或設定編輯內容
        /// </summary>
        public ClassPackage Content
        {
            get
            {
                mPeriods = mTimeTableBusyEditor.GetPeriods();

                List<Appointment> ClassBusys = new List<Appointment>();

                foreach (SchPeriod Period in mPeriods)
                    ClassBusys.Add(Period.ToAppointment());

                mClassPackage.ClassBusys = ClassBusys;

                grdClassBusys.Rows.Clear();

                return mClassPackage;
            }
            set
            {
                #region 清空之前的資料

                grdClassBusys.Rows.Clear();
                #endregion

                if (value != null)
                {
                    mClassPackage = value;

                    var SortClassBusys = from vClassBusy 
                                           in mClassPackage.ClassBusys 
                                           orderby vClassBusy.WeekDay, vClassBusy.BeginTime 
                                           select vClassBusy;

                    mClassPackage.ClassBusys = SortClassBusys.ToList();

                    if (mClassPackage.Class != null)
                        mEditorName = mClassPackage.Class.Name;

                    mPeriods = new List<SchPeriod>();

                    if (!Utility.IsNullOrEmpty(mClassPackage.ClassBusys))
                    {
                        foreach (Appointment vClassBusy in mClassPackage.ClassBusys)
                        {
                            SchPeriod Period = vClassBusy.ToPeriod();
                            mPeriods.Add(Period);

                            int AddRowIndex = grdClassBusys.Rows.Add();
                            DataGridViewRow Row = grdClassBusys.Rows[AddRowIndex];
                            Row.Tag = vClassBusy;

                            Tuple<string, string> DisplayTime = Utility.GetDisplayTime(vClassBusy.BeginTime, vClassBusy.Duration);

                            grdClassBusys.Rows[AddRowIndex].SetValues(
                                vClassBusy.WeekDay,
                                DisplayTime.Item1,
                                DisplayTime.Item2,
                                vClassBusy.Description);
                        }
                    }

                    mTimeTableBusyEditor.SetPeriods(mPeriods);
                }

                IsDirty = false;
            }
        }

        /// <summary>
        /// 將自己傳回
        /// </summary>
        public UserControl Control { get { return this; } }

        /// <summary>
        /// 驗證表單資料是否合法
        /// </summary>
        /// <returns></returns>
        public new bool Validate()
        {
            return true;
        }
        #endregion

        #region DataGrid事件
        /// <summary>
        /// 滑鼠右鍵用來刪除現有記錄
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void grdTimeTableSecs_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            //判斷選取的資料行索引大於0，欄位索引小於0，並且按下滑鼠右鍵
            if (e.RowIndex >= 0 && e.ColumnIndex < 0 && e.Button == MouseButtons.Right)
            {
                //將目前選取的資料行索引記錄下來
                mSelectedRowIndex = e.RowIndex;

                //將目前選取的資料列，除了滑鼠右鍵所在的列外都設為不選取
                foreach (DataGridViewRow var in grdClassBusys.SelectedRows)
                {
                    if (var.Index != mSelectedRowIndex)
                        var.Selected = false;
                }

                //選取目前滑鼠所在的列
                grdClassBusys.Rows[mSelectedRowIndex].Selected = true;

                //顯示滑鼠右鍵選單
                contextMenuStripDelete.Show(grdClassBusys, grdClassBusys.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true).Location);
            }
        }
        #endregion

        private void cmbTimeTables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (mTimeTableBusyEditor == null)
                return;

            if (IsDirty)
                if (MessageBox.Show("您變更的資料尚未儲存，您確定要放棄變更的資料切換時間表？", "排課", MessageBoxButtons.YesNo) == DialogResult.No)
                    return;


            TimeTable vTimeTable = cmbTimeTables.SelectedItem as TimeTable;

            if (vTimeTable != null)
            {
                List<TimeTableSec> Secs = new List<TimeTableSec>();

                foreach (Period vPeriod in vTimeTable.Periods)
                    if (vPeriod.PeriodNo!=0)
                        Secs.Add(vPeriod.ToTimeTableSec());

                mTimeTableBusyEditor.SetTimeTableSecs(Secs);
                mTimeTableBusyEditor.SetPeriods(mPeriods);
            }
        }

        private void tabControl1_DoubleClick(object sender, EventArgs e)
        {
            tabItem2.Visible = !tabItem2.Visible;
        }

        private void panel1_DoubleClick(object sender, EventArgs e)
        {
            tabItem2.Visible = !tabItem2.Visible;
        }
    }
}