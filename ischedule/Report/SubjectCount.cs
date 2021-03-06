﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ischedule
{
    /// <summary>
    /// 報表科目統計
    /// </summary>
    public class SubjectCount
    {
        /// <summary>
        /// 鍵值
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 科目名稱
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// 科目簡稱
        /// </summary>
        public string SubjectAlias { get; set; }

        /// <summary>
        /// 資源名稱
        /// </summary>
        public string ResourceName { get; set; }

        /// <summary>
        /// 節數
        /// </summary>
        public int Len { get; set; }

        public SubjectCount(string Key)
        {
            this.Key = Key;
            this.Subject = string.Empty;
            this.SubjectAlias = string.Empty;
            this.Len = 0;
        }
    }
}