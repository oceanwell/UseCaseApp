using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UseCaseApplication
{
    public partial class Form1: Form
    {
        private bool isDragging = false;
        private Point lastCursor;
        private Point lastForm;

        public Form1()
        {
            InitializeComponent();
            this.Controls.Clear(); // Очищаем все старые элементы
            SetupForm();
        }

        private void SetupForm()
        {
            // Настройки формы как на фотографии
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(43, 43, 43); // #2b2b2b - темный фон как на фото
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.Manual;
            
            // Позиционируем форму поверх основного окна
            Screen screen = Screen.PrimaryScreen;
            int x = (screen.WorkingArea.Width - this.Width) / 2;
            int y = (screen.WorkingArea.Height - this.Height) / 2;
            this.Location = new Point(x, y);
            
            // Делаем форму поверх всех окон
            this.TopMost = true;
            this.BringToFront();
            
            // Добавляем возможность перетаскивания за любую область формы
            this.MouseDown += Form1_MouseDown;
            this.MouseMove += Form1_MouseMove;
            this.MouseUp += Form1_MouseUp;
            
            // Добавляем контент как на фотографии
            SetupContent();
            SetupCloseButton();
        }

        private void SetupContent()
        {
            // Создаем панель для контента
            Panel contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.FromArgb(43, 43, 43);
            this.Controls.Add(contentPanel);
            
            // Заголовок "Документация к UserApplicationCase" - слева как на фото
            Label titleLabel = new Label();
            titleLabel.Text = "Документация к UserApplicationCase";
            titleLabel.Font = new Font("Segoe UI", 36, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(50, 150);
            contentPanel.Controls.Add(titleLabel);
            
            // Подзаголовок "Глава 1" - слева как на фото
            Label chapterLabel = new Label();
            chapterLabel.Text = "Глава 1 (подзаголовок, 24px, medium)";
            chapterLabel.Font = new Font("Segoe UI", 24, FontStyle.Regular);
            chapterLabel.ForeColor = Color.White;
            chapterLabel.BackColor = Color.Transparent;
            chapterLabel.AutoSize = true;
            chapterLabel.Location = new Point(50, 250);
            contentPanel.Controls.Add(chapterLabel);
            
            // Обычный текст - слева как на фото
            Label textLabel = new Label();
            textLabel.Text = "Обычный текст (18px, regular)";
            textLabel.Font = new Font("Segoe UI", 18, FontStyle.Regular);
            textLabel.ForeColor = Color.White;
            textLabel.BackColor = Color.Transparent;
            textLabel.AutoSize = true;
            textLabel.Location = new Point(50, 320);
            contentPanel.Controls.Add(textLabel);
        }

        private void SetupCloseButton()
        {
            // Добавляем желтую кнопку закрытия в правом верхнем углу
            Button closeButton = new Button();
            closeButton.Text = "×";
            closeButton.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            closeButton.ForeColor = Color.White;
            closeButton.BackColor = Color.FromArgb(255, 193, 7); // Золотистый цвет
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Size = new Size(30, 30);
            closeButton.Location = new Point(this.Width - 40, 10);
            closeButton.Click += CloseButton_Click;
            this.Controls.Add(closeButton);
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                lastCursor = Cursor.Position;
                lastForm = this.Location;
            }
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentCursor = Cursor.Position;
                int deltaX = currentCursor.X - lastCursor.X;
                int deltaY = currentCursor.Y - lastCursor.Y;
                this.Location = new Point(lastForm.X + deltaX, lastForm.Y + deltaY);
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        // Пустые методы для совместимости с Form1.Designer.cs
        private void label1_Click(object sender, EventArgs e) { }
        private void button1_Click(object sender, EventArgs e) { }
        private void label3_Click(object sender, EventArgs e) { }
    }
}