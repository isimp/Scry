using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Scry's marks on Valheim's large map step aside while a portal mod's map for choosing a
    /// target is open and that mod hides the map's pins there, as TargetPortal does by its own
    /// *Hide map pins*; with that off, or the portal map shut, they stay.
    /// </summary>
    public class PortalMapTests
    {
        [Fact]
        public void MarksStepAsideOnTheTargetPortalMapThatHidesPins()
        {
            Assert.True(PortalMap.MarksAside(choosing: true, hidesPins: true));
        }

        [Fact]
        public void MarksStayWhereThePortalMapKeepsPinsOrIsShut()
        {
            Assert.False(PortalMap.MarksAside(choosing: true, hidesPins: false));
            Assert.False(PortalMap.MarksAside(choosing: false, hidesPins: true));
            Assert.False(PortalMap.MarksAside(choosing: false, hidesPins: false));
        }

        [Fact]
        public void TheSettingIsOnByItsOwnWord()
        {
            // TargetPortal's Toggle reads "On" or "Off"; anything else, or nothing read, hides nothing.
            Assert.True(PortalMap.HidesPins("On"));
            Assert.False(PortalMap.HidesPins("Off"));
            Assert.False(PortalMap.HidesPins(null));
            Assert.False(PortalMap.HidesPins(""));
        }
    }
}
