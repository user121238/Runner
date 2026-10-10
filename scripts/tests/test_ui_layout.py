"""Static XAML layout contracts; these do not replace on-device measurement or visual QA."""

from pathlib import Path
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
VIEWS = ROOT / "src" / "Runner.Client" / "Views"
MAUI = "{http://schemas.microsoft.com/dotnet/2021/maui}"
XAML = "{http://schemas.microsoft.com/winfx/2009/xaml}"


class RunAndWorkoutLayoutTests(unittest.TestCase):
    def load(self, name):
        return ET.parse(VIEWS / name).getroot()

    def named(self, root, name):
        matches = [node for node in root.iter() if node.get(XAML + "Name") == name]
        self.assertEqual(1, len(matches), f"Expected one named element: {name}")
        return matches[0]

    def parents(self, root):
        return {child: parent for parent in root.iter() for child in parent}

    def assert_natural_height(self, node, parents, boundary):
        while node is not boundary:
            self.assertNotIn("MaximumHeightRequest", node.attrib)
            height = node.get("HeightRequest")
            self.assertTrue(height is None or float(height) < 0, node.attrib)
            self.assertNotIn("MaxLines", node.attrib)
            node = parents[node]

    def test_run_primary_actions_are_outside_the_scrollable_body(self):
        root = self.load("RunView.xaml")
        layout = root.find(MAUI + "Grid")
        self.assertEqual("*,Auto", layout.get("RowDefinitions"))
        body = self.named(root, "RunBody")
        actions = self.named(root, "RunActions")
        self.assertIn(body, list(layout))
        self.assertIn(actions, list(layout))
        self.assertEqual("1", actions.get("Grid.Row"))
        commands = {node.get("Command") for node in actions.iter()}
        self.assertIn("{Binding PrimaryCommand}", commands)
        self.assertIn("{Binding StopCommand}", commands)
        self.assertNotIn(actions, list(body.iter()))

    def test_result_and_selection_are_ordered_natural_flow_siblings(self):
        root = self.load("RunView.xaml")
        parents = self.parents(root)
        body = self.named(root, "RunBody")
        metrics = self.named(root, "SessionMetrics")
        selection = self.named(root, "WorkoutSelection")
        self.assertIs(parents[metrics], parents[selection])
        self.assertEqual(MAUI + "VerticalStackLayout", parents[metrics].tag)
        siblings = list(parents[metrics])
        self.assertLess(siblings.index(metrics), siblings.index(selection))
        self.assertNotEqual("Center", metrics.get("VerticalOptions"))
        self.assert_natural_height(metrics, parents, body)
        self.assert_natural_height(selection, parents, body)
        for grid in body.iter(MAUI + "Grid"):
            rows = grid.get("RowDefinitions", "")
            self.assertNotIn("*", rows, "Scrollable content must not reserve a shrinking star row")

    def test_run_error_wraps_without_consuming_the_fixed_action_row(self):
        root = self.load("RunView.xaml")
        parents = self.parents(root)
        body = self.named(root, "RunBody")
        panel = self.named(root, "RunMessage")
        self.assertIn(panel, list(body.iter()))
        label = next(node for node in panel.iter() if node.get("Text") == "{Binding Message}")
        self.assertEqual("CharacterWrap", label.get("LineBreakMode"))
        self.assert_natural_height(label, parents, body)
        copy = next(node for node in panel.iter() if node.get("Command") == "{Binding CopyErrorDetailsCommand}")
        self.assertEqual("{Binding HasErrorDetails}", copy.get("IsVisible"))
        self.assertFalse(any(node.get("Text") == "{Binding Message}"
                             for node in self.named(root, "RunActions").iter()))

    def test_summary_and_course_choices_can_grow_with_font_size(self):
        root = self.load("RunView.xaml")
        parents = self.parents(root)
        body = self.named(root, "RunBody")
        summary = self.named(root, "WorkoutSummary")
        self.assert_natural_height(summary, parents, body)
        for label in summary.iter(MAUI + "Label"):
            self.assertEqual("WordWrap", label.get("LineBreakMode"))
            self.assert_natural_height(label, parents, body)
        choices = next(node for node in body.iter()
                       if node.get("BindableLayout.ItemsSource") == "{Binding Choices}")
        self.assertEqual(MAUI + "FlexLayout", choices.tag)
        self.assertEqual("Wrap", choices.get("Wrap"))
        for border in choices.iter(MAUI + "Border"):
            self.assertEqual("-1", border.get("HeightRequest"), "Override the old fixed-height Chip style")

    def test_workout_loading_error_and_empty_states_scroll_with_the_list(self):
        root = self.load("WorkoutsView.xaml")
        layout = root.find(MAUI + "Grid")
        self.assertEqual("*,Auto", layout.get("RowDefinitions"))
        collection = self.named(root, "WorkoutList")
        self.assertIn(collection, list(layout))
        self.assertIsNone(collection.find(MAUI + "CollectionView.EmptyView"))
        header = collection.find(MAUI + "CollectionView.Header")
        self.assertIsNotNone(header)
        empty = self.named(root, "WorkoutEmpty")
        error = self.named(root, "WorkoutError")
        self.assertIn(empty, list(header.iter()))
        self.assertIn(error, list(header.iter()))
        self.assertEqual("{Binding ShowEmpty}", empty.get("IsVisible"))
        self.assertEqual("{Binding HasMessage}", error.get("IsVisible"))
        self.assertTrue(any(node.get("IsVisible") == "{Binding IsLoading}" for node in header.iter()))
        self.assertTrue(any(node.get("Command") == "{Binding RetryCommand}" for node in error.iter()))
        self.assertTrue(any(node.get("Command") == "{Binding CopyErrorDetailsCommand}" for node in error.iter()))
        create = self.named(root, "CreateWorkout")
        self.assertIn(create, list(layout))
        self.assertEqual("1", create.get("Grid.Row"))
        self.assertEqual("{Binding CreateCommand}", create.get("Command"))

    def test_workout_error_is_unbounded_and_empty_copy_is_platform_neutral(self):
        root = self.load("WorkoutsView.xaml")
        parents = self.parents(root)
        header = self.named(root, "WorkoutList").find(MAUI + "CollectionView.Header")
        error = self.named(root, "WorkoutError")
        label = next(node for node in error.iter() if node.get("Text") == "{Binding Message}")
        self.assertEqual("CharacterWrap", label.get("LineBreakMode"))
        self.assert_natural_height(label, parents, header)
        empty_text = " ".join(node.get("Text", "") for node in self.named(root, "WorkoutEmpty").iter())
        self.assertNotIn("自动切歌", empty_text)
        self.assertIn("自动切换目标步频", empty_text)


if __name__ == "__main__":
    unittest.main()
