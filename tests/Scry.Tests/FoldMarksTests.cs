using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class FoldMarksTests
    {
        // With the compendium on, LINKED holds a page's own parts and wiring, nothing to find: it
        // stands folded until the player opens it, and stays as the player leaves it, kept with
        // the panel's other folds. Every other section folds as before.

        [Fact]
        public void LinkedWithTheCompendiumOnStandsFoldedUntilOpenedAndKeepsThePlayersChoice()
        {
            var marks = new HashSet<string>();
            Assert.True(FoldMarks.Folded(marks, FoldMarks.LinkedInBook));
            FoldMarks.Set(marks, FoldMarks.LinkedInBook, false);
            Assert.False(FoldMarks.Folded(marks, FoldMarks.LinkedInBook));

            // Kept as the panel keeps its folds: the same marks read again.
            var kept = new HashSet<string>(marks);
            Assert.False(FoldMarks.Folded(kept, FoldMarks.LinkedInBook));
            FoldMarks.Set(kept, FoldMarks.LinkedInBook, true);
            Assert.True(FoldMarks.Folded(kept, FoldMarks.LinkedInBook));
        }

        [Fact]
        public void LinkedWithTheCompendiumOffAndOtherSectionsStandOpenUntilFolded()
        {
            var marks = new HashSet<string>();
            Assert.False(FoldMarks.Folded(marks, "links"));
            Assert.False(FoldMarks.Folded(marks, "facts"));
            FoldMarks.Set(marks, "links", true);
            Assert.True(FoldMarks.Folded(marks, "links"));
            Assert.True(FoldMarks.Folded(marks, FoldMarks.LinkedInBook));
            Assert.False(FoldMarks.Folded(marks, null));
        }

        [Fact]
        public void FoldingOrOpeningEverySectionTakesLinkedAlong()
        {
            var sections = new[] { "facts", "links", FoldMarks.LinkedInBook };
            var marks = new HashSet<string> { "cardcreaturesopen" };
            FoldMarks.All(marks, sections, false);
            foreach (var section in sections) Assert.False(FoldMarks.Folded(marks, section));
            // Opening every one lets go of every other mark, as it always has.
            Assert.DoesNotContain("cardcreaturesopen", marks);
            FoldMarks.All(marks, sections, true);
            foreach (var section in sections) Assert.True(FoldMarks.Folded(marks, section));
        }
    }
}
