﻿namespace RainbowMage.OverlayPlugin.Overlays
{
    partial class MiniParseEventSourceConfigPanel
    {
        /// <summary> 
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region コンポーネント デザイナーで生成されたコード

        /// <summary> 
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を 
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MiniParseEventSourceConfigPanel));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.comboSortKey = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textUpdateInterval = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.checkSortDesc = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.comboSortKey, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.textUpdateInterval, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.checkSortDesc, 1, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // comboSortKey
            // 
            resources.ApplyResources(this.comboSortKey, "comboSortKey");
            this.comboSortKey.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSortKey.FormattingEnabled = true;
            this.comboSortKey.Name = "comboSortKey";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textUpdateInterval
            // 
            resources.ApplyResources(this.textUpdateInterval, "textUpdateInterval");
            this.textUpdateInterval.Name = "textUpdateInterval";
            this.textUpdateInterval.Leave += new System.EventHandler(this.TextUpdateInterval_Leave);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // checkSortDesc
            // 
            resources.ApplyResources(this.checkSortDesc, "checkSortDesc");
            this.checkSortDesc.Name = "checkSortDesc";
            this.checkSortDesc.UseVisualStyleBackColor = true;
            this.checkSortDesc.CheckedChanged += new System.EventHandler(this.CheckSortDesc_CheckedChanged);
            // 
            // MiniParseEventSourceConfigPanel
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "MiniParseEventSourceConfigPanel";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.ComboBox comboSortKey;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textUpdateInterval;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox checkSortDesc;
    }
}
