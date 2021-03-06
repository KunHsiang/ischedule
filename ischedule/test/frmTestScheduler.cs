﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Sunset.Data;
using System.Diagnostics;

namespace ischedule.test
{
    public partial class frmTestScheduler : BaseForm
    {
        private Scheduler schLocal = Scheduler.Instance;
        private JSScriptManager jsmgr;

        public frmTestScheduler()
        {
            InitializeComponent();
        }

        private void frmTestScheduler_Load(object sender, EventArgs e)
        {
            this.jsmgr = new JSScriptManager(this.webBrowser1, SchedulerType.Teacher);

            if (!schLocal.IsOpen)
            {
                schLocal.ImportSourceComplete += new EventHandler<Scheduler.ImportSourceCompleteEventArgs>(schLocal_ImportSourceComplete);
                schLocal.Open("C:\\101學年度第2學期0210.xml");
            }
            else
            {
                this.schLocal_ImportSourceComplete(null, null);
            }
            
        }

        void schLocal_ImportSourceComplete(object sender, Scheduler.ImportSourceCompleteEventArgs e)
        {
            this.listBox1.Items.Clear();
            foreach (Teacher t in this.schLocal.Teachers)
            {
                TeacherWrapper tw = new TeacherWrapper(t);
                string target = tw.ToString();
                if (!string.IsNullOrEmpty(tw.ToString()))
                    this.listBox1.Items.Add(tw);

            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Teacher t = ((TeacherWrapper)this.listBox1.SelectedItem).Teacher;
            List<CEvent> events = Scheduler.Instance.CEvents.GetByTeacherID(t.TeacherID);
            this.dataGridViewX1.DataSource = events;
        }

        private void dataGridViewX1_SelectionChanged(object sender, EventArgs e)
        {
            if (this.dataGridViewX1.SelectedRows.Count > 0)
            {
                DataGridViewRow dr = this.dataGridViewX1.SelectedRows[0];
                string eventID = dr.Cells[0].Value.ToString();
                showTeacherSchedule(eventID);
            }
        }

        //顯示教師課表
        private void showTeacherSchedule(string eventID)
        {
            Stopwatch w = new Stopwatch();
            w.Start();
            //0. 取得 CEvent 物件
            CEvent evt = Scheduler.Instance.CEvents[eventID];
            if (evt == null)
            {
                MessageBox.Show("CEvent does not exist !");
                return;
            }

            //1. 取得 TimeTable 物件
            TimeTable tbl = Scheduler.Instance.TimeTables[evt.TimeTableID];
            this.jsmgr.SetTimeTable(tbl);

            //3. 取得 這位教師的所有課程分段
            List<CEvent> evts = Scheduler.Instance.CEvents.GetByTeacherID(evt.TeacherID1);
            this.jsmgr.SetCourseSection(evts);
            //2. 取得 Teacher Busy 時段
            Teacher t = Scheduler.Instance.Teachers[evt.TeacherID1];
            List<Period> busyPeriods = (t != null) ? t.GetBusyPeriods(evt.TimeTableID) : (new List<Period>());  //不排課時段占用的TimeTable節次清單
            this.jsmgr.SetBusyPeriods(busyPeriods);

            w.Stop();
            Console.WriteLine("Time : {0}", w.ElapsedMilliseconds.ToString());
            
            //4. 取得此課程分段的資源衝突時段

            //5. 取得此課程分段的可排課時段
        }
    }
}
