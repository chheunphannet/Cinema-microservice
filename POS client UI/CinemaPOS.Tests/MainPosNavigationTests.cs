using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using CinemaPOS.App.Forms;
using Xunit;

namespace CinemaPOS.Tests
{
    public class MainPosNavigationTests
    {
        private static IEnumerable<Control> GetAllControls(Control parent)
        {
            yield return parent;
            foreach (Control child in parent.Controls)
            {
                foreach (Control grandChild in GetAllControls(child))
                {
                    yield return grandChild;
                }
            }
        }

        private static bool IsSelfVisible(Control c)
        {
            var m = typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
            if (m != null)
            {
                return (bool)m.Invoke(c, new object[] { 2 /* STATE_VISIBLE */ })!;
            }
            return c.Visible;
        }

        [Fact]
        public void MainPosForm_LeftNavPanel_ContainsMoviesButtonWithCorrectProperties()
        {
            using var form = new MainPosForm();

            var allButtons = GetAllControls(form).OfType<Button>().ToList();
            var movieBtn = allButtons.FirstOrDefault(b => b.Name == "btnNav_Movie" || (b.Tag is string tag && tag == "Movie"));

            Assert.NotNull(movieBtn);
            Assert.Equal("Movies", movieBtn.Text);
            Assert.Equal("Movie", movieBtn.Tag);
            Assert.True(IsSelfVisible(movieBtn));
        }

        [Fact]
        public void MainPosForm_LeftNavPanel_ButtonsAreInExpectedOrder()
        {
            using var form = new MainPosForm();

            // Find the nav FlowLayoutPanel
            var navStack = GetAllControls(form)
                .OfType<FlowLayoutPanel>()
                .FirstOrDefault(flp => flp.Controls.OfType<Button>().Any(b => b.Tag is string t && t == "Movie"));

            Assert.NotNull(navStack);

            var navButtons = navStack.Controls.OfType<Button>().ToList();
            Assert.Equal(4, navButtons.Count);

            Assert.Equal("Movie", navButtons[0].Tag);
            Assert.Equal("SeatMap", navButtons[1].Tag);
            Assert.Equal("F&B", navButtons[2].Tag);
            Assert.Equal("Reservation", navButtons[3].Tag);
        }

        [Fact]
        public void MainPosForm_DefaultActiveNavView_IsMovie()
        {
            using var form = new MainPosForm();

            var activeField = typeof(MainPosForm).GetField("_activeNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(activeField);
            var activeVal = activeField.GetValue(form) as string;

            Assert.Equal("Movie", activeVal);

            var movieGridPanel = typeof(MainPosForm).GetField("_movieGridPanel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            Assert.NotNull(movieGridPanel);
            Assert.True(IsSelfVisible(movieGridPanel));
        }

        [Fact]
        public void MainPosForm_SwitchNavView_SwitchesBetweenViewsAndHighlightsMovie()
        {
            using var form = new MainPosForm();

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(switchMethod);

            var activeField = typeof(MainPosForm).GetField("_activeNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(activeField);

            var movieGridPanel = typeof(MainPosForm).GetField("_movieGridPanel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var fnbPanel = typeof(MainPosForm).GetField("_fnbContainerPanel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var seatMapPanel = typeof(MainPosForm).GetField("_seatMapContainerPanel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var resPanel = typeof(MainPosForm).GetField("_reservationPanel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;

            Assert.NotNull(movieGridPanel);
            Assert.NotNull(fnbPanel);
            Assert.NotNull(seatMapPanel);
            Assert.NotNull(resPanel);

            // Switch to F&B
            switchMethod.Invoke(form, new object[] { "F&B" });
            Assert.Equal("F&B", activeField.GetValue(form));
            Assert.False(IsSelfVisible(movieGridPanel));
            Assert.True(IsSelfVisible(fnbPanel));

            // Switch to SeatMap
            switchMethod.Invoke(form, new object[] { "SeatMap" });
            Assert.Equal("SeatMap", activeField.GetValue(form));
            Assert.False(IsSelfVisible(movieGridPanel));
            Assert.True(IsSelfVisible(seatMapPanel));

            // Switch to Reservation
            switchMethod.Invoke(form, new object[] { "Reservation" });
            Assert.Equal("Reservation", activeField.GetValue(form));
            Assert.False(IsSelfVisible(movieGridPanel));
            Assert.True(IsSelfVisible(resPanel));

            // Switch back to Movie via Movie nav button click simulation
            var movieBtn = GetAllControls(form).OfType<Button>().FirstOrDefault(b => b.Name == "btnNav_Movie");
            Assert.NotNull(movieBtn);
            var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance);
            onClick?.Invoke(movieBtn, new object[] { EventArgs.Empty });

            Assert.Equal("Movie", activeField.GetValue(form));
            Assert.True(IsSelfVisible(movieGridPanel));
            Assert.False(IsSelfVisible(fnbPanel));
            Assert.False(IsSelfVisible(seatMapPanel));
            Assert.False(IsSelfVisible(resPanel));
        }

        [Fact]
        public void MainPosForm_KeyDown_F10_SwitchesToMovie()
        {
            using var form = new MainPosForm();

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeField = typeof(MainPosForm).GetField("_activeNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            var keyDownMethod = typeof(MainPosForm).GetMethod("MainPosForm_KeyDown", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(switchMethod);
            Assert.NotNull(activeField);
            Assert.NotNull(keyDownMethod);

            // First switch away from Movie
            switchMethod.Invoke(form, new object[] { "F&B" });
            Assert.Equal("F&B", activeField.GetValue(form));

            // Press F10
            var ke = new KeyEventArgs(Keys.F10);
            keyDownMethod.Invoke(form, new object[] { form, ke });

            Assert.Equal("Movie", activeField.GetValue(form));
            Assert.True(ke.Handled);
        }

        [Fact]
        public void MainPosForm_KeyDown_F5_SwitchesToSeatMap()
        {
            using var form = new MainPosForm();

            var activeField = typeof(MainPosForm).GetField("_activeNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            var keyDownMethod = typeof(MainPosForm).GetMethod("MainPosForm_KeyDown", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(activeField);
            Assert.NotNull(keyDownMethod);

            Assert.Equal("Movie", activeField.GetValue(form));

            // Press F5
            var ke = new KeyEventArgs(Keys.F5);
            keyDownMethod.Invoke(form, new object[] { form, ke });

            Assert.Equal("SeatMap", activeField.GetValue(form));
            Assert.True(ke.Handled);
        }

        [Fact]
        public void MainPosForm_BrandLogo_Click_SwitchesToMovie()
        {
            using var form = new MainPosForm();

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            var activeField = typeof(MainPosForm).GetField("_activeNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            var onClick = typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(switchMethod);
            Assert.NotNull(activeField);
            Assert.NotNull(onClick);

            // Switch to SeatMap
            switchMethod.Invoke(form, new object[] { "SeatMap" });
            Assert.Equal("SeatMap", activeField.GetValue(form));

            // Find brand title label
            var allControls = GetAllControls(form).ToList();
            var brandTitle = allControls.OfType<Label>().FirstOrDefault(l => l.Text.Equals("POS Cinema", StringComparison.OrdinalIgnoreCase) || l.Text == "CINEMA POS");
            Assert.NotNull(brandTitle);

            onClick.Invoke(brandTitle, new object[] { EventArgs.Empty });
            Assert.Equal("Movie", activeField.GetValue(form));
        }

        [Theory]
        [InlineData("Movie", "#2563EB", "#1E293B", "#1E293B", "#1E293B")]
        [InlineData("SeatMap", "#1E293B", "#2563EB", "#1E293B", "#1E293B")]
        [InlineData("F&B", "#1E293B", "#1E293B", "#2563EB", "#1E293B")]
        [InlineData("Reservation", "#1E293B", "#1E293B", "#1E293B", "#2563EB")]
        public void MainPosForm_NavButtons_HighlightActiveViewCorrectly(
            string targetView, string expectedMovieColor, string expectedSeatColor, string expectedFnbColor, string expectedResColor)
        {
            using var form = new MainPosForm();

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { targetView });

            var allButtons = GetAllControls(form).OfType<Button>().ToList();
            var movieBtn = allButtons.First(b => b.Name == "btnNav_Movie");
            var seatBtn = allButtons.First(b => b.Name == "btnNav_SeatMap");
            var fnbBtn = allButtons.First(b => b.Name == "btnNav_F&B");
            var resBtn = allButtons.First(b => b.Name == "btnNav_Reservation");

            Assert.Equal(ColorTranslator.FromHtml(expectedMovieColor), movieBtn.BackColor);
            Assert.Equal(ColorTranslator.FromHtml(expectedSeatColor), seatBtn.BackColor);
            Assert.Equal(ColorTranslator.FromHtml(expectedFnbColor), fnbBtn.BackColor);
            Assert.Equal(ColorTranslator.FromHtml(expectedResColor), resBtn.BackColor);
        }

        [Fact]
        public void MainPosForm_LeftNavPanel_BrandHeader_IsVisibleAndAboveNavStack()
        {
            using var form = new MainPosForm();
            form.Size = new System.Drawing.Size(1280, 800);
            form.CreateControl();

            var allControls = GetAllControls(form).ToList();
            var brandTitle = allControls.OfType<Label>().FirstOrDefault(l => l.Text.Equals("POS Cinema", StringComparison.OrdinalIgnoreCase) || l.Text == "CINEMA POS");
            var navStack = allControls.OfType<FlowLayoutPanel>().FirstOrDefault(flp => flp.Controls.OfType<Button>().Any(b => b.Tag is string t && t == "Movie"));

            Assert.NotNull(brandTitle);
            Assert.NotNull(navStack);

            var brandContainer = brandTitle.Parent;
            Assert.NotNull(brandContainer);
            Assert.True(IsSelfVisible(brandContainer));

            // Verify brand header sits above the navStack and is not covered
            Assert.True(brandContainer.Top < navStack.Top, $"Expected brandContainer.Top ({brandContainer.Top}) < navStack.Top ({navStack.Top})");
            Assert.True(brandContainer.Bottom <= navStack.Top, $"Expected brandContainer.Bottom ({brandContainer.Bottom}) <= navStack.Top ({navStack.Top})");
        }

        [Fact]
        public void MainPosForm_NavButtons_InitialMovieButton_IsActiveOnCreation()
        {
            using var form = new MainPosForm();

            var allButtons = GetAllControls(form).OfType<Button>().ToList();
            var movieBtn = allButtons.First(b => b.Name == "btnNav_Movie");
            var seatBtn = allButtons.First(b => b.Name == "btnNav_SeatMap");

            // Movie button must be initialized with active color #2563EB immediately upon construction
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#2563EB"), movieBtn.BackColor);
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#1E293B"), seatBtn.BackColor);
        }

        [Fact]
        public void MainPosForm_SwitchToSeatMap_WithoutShowtime_ShowsGuidance()
        {
            using var form = new MainPosForm();

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { "SeatMap" });

            var seatMapHeaderLabel = typeof(MainPosForm).GetField("_seatMapHeaderLabel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Label;
            Assert.NotNull(seatMapHeaderLabel);
            Assert.Contains("No Showtime Selected", seatMapHeaderLabel.Text);
            Assert.Contains("Movies [F10]", seatMapHeaderLabel.Text);
        }

        [Fact]
        public void MainPosForm_SeatMapHeader_ControlsDoNotOverlap_InSingleRowMode()
        {
            using var form = new MainPosForm();
            form.Size = new System.Drawing.Size(1920, 1080);

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { "SeatMap" });

            var seatMapHeader = typeof(MainPosForm).GetField("_seatMapHeader", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var btnBack = typeof(MainPosForm).GetField("_btnBackToMovies", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Button;
            var headerLabel = typeof(MainPosForm).GetField("_seatMapHeaderLabel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Label;
            var demoFlow = typeof(MainPosForm).GetField("_seatDemographicsFlow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as FlowLayoutPanel;
            var updateLayoutMethod = typeof(MainPosForm).GetMethod("UpdateSeatMapHeaderLayout", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(seatMapHeader);
            Assert.NotNull(btnBack);
            Assert.NotNull(headerLabel);
            Assert.NotNull(demoFlow);

            // Set simulated wide header width and movie title
            seatMapHeader.Size = new System.Drawing.Size(1200, 48);
            headerLabel.Text = "ALL WISHES COME TRUE  |  Hall 3 (VIP IMAX)  |  20:15";

            // Add standard demographic buttons
            demoFlow.Controls.Clear();
            demoFlow.Controls.Add(new Button { Text = "Adult (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Child (Under 12) (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Senior (65+) (-$2.00)", AutoSize = true, Height = 34 });

            updateLayoutMethod?.Invoke(form, null);

            Assert.Equal(48, seatMapHeader.Height);
            Assert.False(headerLabel.Bounds.IntersectsWith(demoFlow.Bounds), "Movie title label must not overlap demographic buttons");
            Assert.False(headerLabel.Bounds.IntersectsWith(btnBack.Bounds), "Movie title label must not overlap back button");
            Assert.False(btnBack.Bounds.IntersectsWith(demoFlow.Bounds), "Back button must not overlap demographic buttons");

            // Label must be positioned cleanly between Back button and Demographics flow
            Assert.True(headerLabel.Left >= btnBack.Right, "Label should start after back button");
            Assert.True(headerLabel.Right <= demoFlow.Left, "Label should end before demographic buttons");
        }

        [Fact]
        public void MainPosForm_SeatMapHeader_ControlsDoNotOverlap_InTwoTierMode()
        {
            using var form = new MainPosForm();
            form.Size = new System.Drawing.Size(1250, 900); // 650px center area

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { "SeatMap" });

            var seatMapHeader = typeof(MainPosForm).GetField("_seatMapHeader", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var btnBack = typeof(MainPosForm).GetField("_btnBackToMovies", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Button;
            var headerLabel = typeof(MainPosForm).GetField("_seatMapHeaderLabel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Label;
            var demoFlow = typeof(MainPosForm).GetField("_seatDemographicsFlow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as FlowLayoutPanel;
            var updateLayoutMethod = typeof(MainPosForm).GetMethod("UpdateSeatMapHeaderLayout", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(seatMapHeader);
            Assert.NotNull(btnBack);
            Assert.NotNull(headerLabel);
            Assert.NotNull(demoFlow);

            // Narrow width simulates compact screen where all controls cannot fit on 1 row
            headerLabel.Text = "ALL WISHES COME TRUE  |  Hall 3 (VIP IMAX)  |  20:15";

            demoFlow.Controls.Clear();
            demoFlow.Controls.Add(new Button { Text = "Adult (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Child (Under 12) (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Senior (65+) (-$2.00)", AutoSize = true, Height = 34 });

            updateLayoutMethod?.Invoke(form, null);

            // In narrow mode, header expands to two tiers
            Assert.Equal(84, seatMapHeader.Height);
            Assert.False(headerLabel.Bounds.IntersectsWith(demoFlow.Bounds), "Label must not overlap demographic flow in two-tier mode");
            Assert.False(headerLabel.Bounds.IntersectsWith(btnBack.Bounds), "Label must not overlap back button in two-tier mode");
            Assert.True(demoFlow.Top >= btnBack.Bottom, "Demographics flow must be placed below the top row");
        }

        [Fact]
        public void MainPosForm_SeatMapHeader_TwoTierMode_AccommodatesOverflowDemographicsWithoutVerticalScroll()
        {
            using var form = new MainPosForm();
            form.Size = new System.Drawing.Size(950, 700); // Narrow viewport: center area ~350px

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { "SeatMap" });

            var seatMapHeader = typeof(MainPosForm).GetField("_seatMapHeader", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var btnBack = typeof(MainPosForm).GetField("_btnBackToMovies", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Button;
            var headerLabel = typeof(MainPosForm).GetField("_seatMapHeaderLabel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Label;
            var demoFlow = typeof(MainPosForm).GetField("_seatDemographicsFlow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as FlowLayoutPanel;
            var updateLayoutMethod = typeof(MainPosForm).GetMethod("UpdateSeatMapHeaderLayout", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(seatMapHeader);
            Assert.NotNull(btnBack);
            Assert.NotNull(headerLabel);
            Assert.NotNull(demoFlow);

            headerLabel.Text = "SUPER BLOCKBUSTER TITLE: EXTENDED EDITION  |  Hall 1  |  21:45";

            // Add many demographic pills that exceed the narrow row 2 width
            demoFlow.Controls.Clear();
            demoFlow.Controls.Add(new Button { Text = "Adult (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Child (Under 12) (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Senior (65+) (-$2.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Student (-$1.50)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "VIP Recliner (+$3.00)", AutoSize = true, Height = 34 });

            updateLayoutMethod?.Invoke(form, null);

            // Flow height must expand to accommodate horizontal scrollbar without clipping 34px buttons
            int expectedMinFlowH = 34 + 2 + SystemInformation.HorizontalScrollBarHeight;
            Assert.True(demoFlow.Height >= expectedMinFlowH, $"Flow panel height {demoFlow.Height} should accommodate scrollbar (>= {expectedMinFlowH})");
            Assert.True(seatMapHeader.Height > 84, $"Header height {seatMapHeader.Height} should expand beyond 84px to prevent clipping");
            Assert.True(demoFlow.AutoScroll, "AutoScroll should be enabled when buttons exceed available width");

            Assert.False(headerLabel.Bounds.IntersectsWith(demoFlow.Bounds));
            Assert.False(headerLabel.Bounds.IntersectsWith(btnBack.Bounds));
            Assert.True(demoFlow.Top >= btnBack.Bottom);
        }

        [Fact]
        public void MainPosForm_SeatMapHeader_AutomaticallyUpdatesLayout_WhenHeaderTextChanges()
        {
            using var form = new MainPosForm();
            form.Size = new System.Drawing.Size(1920, 1080);

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { "SeatMap" });

            var seatMapHeader = typeof(MainPosForm).GetField("_seatMapHeader", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var headerLabel = typeof(MainPosForm).GetField("_seatMapHeaderLabel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Label;
            var demoFlow = typeof(MainPosForm).GetField("_seatDemographicsFlow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as FlowLayoutPanel;

            Assert.NotNull(seatMapHeader);
            Assert.NotNull(headerLabel);
            Assert.NotNull(demoFlow);

            // Add pills
            demoFlow.Controls.Clear();
            demoFlow.Controls.Add(new Button { Text = "Adult (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Child (+$0.00)", AutoSize = true, Height = 34 });

            // Changing label text directly should fire TextChanged event and update layout automatically without manual invoke
            headerLabel.Text = "UPDATED TITLE FROM TEXTCHANGED EVENT  |  Auditorium 2  |  18:30";

            Assert.Equal(48, seatMapHeader.Height);
            Assert.False(headerLabel.Bounds.IntersectsWith(demoFlow.Bounds));
            Assert.True(headerLabel.Right <= demoFlow.Left);
        }

        [Fact]
        public void MainPosForm_SeatMapHeader_NoDemographicButtons_RemainsSingleRow()
        {
            using var form = new MainPosForm();
            form.Size = new System.Drawing.Size(1250, 900);

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { "SeatMap" });

            var seatMapHeader = typeof(MainPosForm).GetField("_seatMapHeader", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var btnBack = typeof(MainPosForm).GetField("_btnBackToMovies", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Button;
            var headerLabel = typeof(MainPosForm).GetField("_seatMapHeaderLabel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Label;
            var demoFlow = typeof(MainPosForm).GetField("_seatDemographicsFlow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as FlowLayoutPanel;

            Assert.NotNull(seatMapHeader);
            Assert.NotNull(btnBack);
            Assert.NotNull(headerLabel);
            Assert.NotNull(demoFlow);

            demoFlow.Controls.Clear();
            headerLabel.Text = "ONLY MOVIE TITLE WITH NO DEMOGRAPHIC BUTTONS";

            // With no demographic buttons, header remains clean single-row (Height = 48)
            Assert.Equal(48, seatMapHeader.Height);
            Assert.False(headerLabel.Bounds.IntersectsWith(btnBack.Bounds));
            Assert.True(headerLabel.Left >= btnBack.Right);
        }

        [Fact]
        public void MainPosForm_SeatMapHeader_RapidTitleChanges_LongTitles_NeverOverlap()
        {
            using var form = new MainPosForm();
            form.Size = new System.Drawing.Size(1366, 768); // Standard POS monitor

            var switchMethod = typeof(MainPosForm).GetMethod("SwitchNavView", BindingFlags.NonPublic | BindingFlags.Instance);
            switchMethod?.Invoke(form, new object[] { "SeatMap" });

            var seatMapHeader = typeof(MainPosForm).GetField("_seatMapHeader", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Panel;
            var btnBack = typeof(MainPosForm).GetField("_btnBackToMovies", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Button;
            var headerLabel = typeof(MainPosForm).GetField("_seatMapHeaderLabel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as Label;
            var demoFlow = typeof(MainPosForm).GetField("_seatDemographicsFlow", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(form) as FlowLayoutPanel;

            Assert.NotNull(seatMapHeader);
            Assert.NotNull(btnBack);
            Assert.NotNull(headerLabel);
            Assert.NotNull(demoFlow);

            demoFlow.Controls.Clear();
            demoFlow.Controls.Add(new Button { Text = "Adult (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Child (Under 12) (+$0.00)", AutoSize = true, Height = 34 });
            demoFlow.Controls.Add(new Button { Text = "Senior (65+) (-$2.00)", AutoSize = true, Height = 34 });

            string[] testTitles = new[]
            {
                "UP",
                "AVATAR: THE WAY OF WATER  |  Hall 3 (VIP IMAX)  |  20:15",
                "THE LORD OF THE RINGS: THE RETURN OF THE KING (EXTENDED EDITION)  |  Grand Cinema 1  |  23:45",
                "SHORT",
                "A VERY VERY LONG TITLE THAT EXCEEDS SEVENTY CHARACTERS IN TOTAL LENGTH  |  Auditorium 4  |  14:00"
            };

            foreach (var title in testTitles)
            {
                headerLabel.Text = title;

                if (seatMapHeader.Height == 48)
                {
                    Assert.False(headerLabel.Bounds.IntersectsWith(demoFlow.Bounds));
                    Assert.False(headerLabel.Bounds.IntersectsWith(btnBack.Bounds));
                    Assert.True(headerLabel.Left >= btnBack.Right);
                    Assert.True(headerLabel.Right <= demoFlow.Left);
                }
                else
                {
                    Assert.False(headerLabel.Bounds.IntersectsWith(demoFlow.Bounds));
                    Assert.False(headerLabel.Bounds.IntersectsWith(btnBack.Bounds));
                    Assert.True(demoFlow.Top >= btnBack.Bottom);
                }
            }
        }
    }
}
