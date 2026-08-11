using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Moq;
using OCC.WpfClient.Infrastructure.AttachedProperties;
using Xunit;

namespace OCC.Tests.Infrastructure
{
    /// <summary>
    /// Unit tests for <see cref="CommandProperties"/> attached properties.
    /// </summary>
    public class CommandPropertiesTests
    {
        private void RunInSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        [Fact]
        public void IsFilteredProperty_GetterAndSetter_WorkCorrectly()
        {
            RunInSta(() =>
            {
                var comboBox = new ComboBox();
                CommandProperties.SetIsFiltered(comboBox, true);

                Assert.True(CommandProperties.GetIsFiltered(comboBox));
            });
        }

        [Fact]
        public void HideArrowProperty_GetterAndSetter_WorkCorrectly()
        {
            RunInSta(() =>
            {
                var comboBox = new ComboBox();
                CommandProperties.SetHideArrow(comboBox, true);

                Assert.True(CommandProperties.GetHideArrow(comboBox));
            });
        }

        [Fact]
        public void LostFocusCommandProperty_GetterAndSetter_WorkCorrectly()
        {
            RunInSta(() =>
            {
                var comboBox = new ComboBox();
                var mockCommand = new Mock<ICommand>();
                CommandProperties.SetLostFocusCommand(comboBox, mockCommand.Object);

                Assert.Equal(mockCommand.Object, CommandProperties.GetLostFocusCommand(comboBox));
            });
        }

        [Fact]
        public void SelectionChangedCommandProperty_GetterAndSetter_WorkCorrectly()
        {
            RunInSta(() =>
            {
                var comboBox = new ComboBox();
                var mockCommand = new Mock<ICommand>();
                CommandProperties.SetSelectionChangedCommand(comboBox, mockCommand.Object);

                Assert.Equal(mockCommand.Object, CommandProperties.GetSelectionChangedCommand(comboBox));
            });
        }

        [Fact]
        public void DoubleClickCommandProperty_GetterAndSetter_WorkCorrectly()
        {
            RunInSta(() =>
            {
                var control = new Control();
                var mockCommand = new Mock<ICommand>();
                CommandProperties.SetDoubleClickCommand(control, mockCommand.Object);

                Assert.Equal(mockCommand.Object, CommandProperties.GetDoubleClickCommand(control));
            });
        }

        [Fact]
        public void CommandParameterProperty_GetterAndSetter_WorkCorrectly()
        {
            RunInSta(() =>
            {
                var comboBox = new ComboBox();
                var param = new object();
                CommandProperties.SetCommandParameter(comboBox, param);

                Assert.Equal(param, CommandProperties.GetCommandParameter(comboBox));
            });
        }
    }
}
