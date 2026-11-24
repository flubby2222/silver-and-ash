"""Selbsttest für tools/models/checksums.py (Issue #636), nur Standardbibliothek, ohne Blender und ohne dotnet.

Aufruf aus dem Repository-Wurzelverzeichnis:
    python tools/models/checksums_test.py

Jeder Test baut einen eigenen Wegwerf-Ordner mit den drei Asset-Ordnern; das echte assets/ wird nie angefasst.
"""

import contextlib
import io
import os
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import checksums  # noqa: E402


class ChecksumsTests(unittest.TestCase):
    def setUp(self):
        self.assets = tempfile.mkdtemp(prefix="checksums-test-")
        self.addCleanup(shutil.rmtree, self.assets)
        for folder, _, _, _ in checksums.LISTS:
            os.makedirs(os.path.join(self.assets, folder))
        self.put("models/ghoul.glb", b"glb bytes")
        self.put("models/ghoul.png", b"png bytes")
        self.put("textures/wall.png", b"wall")
        self.put("audio/hit.wav", b"RIFF")
        self.quiet(checksums.write_lists, self.assets)

    def put(self, relative, data):
        with open(os.path.join(self.assets, relative), "wb") as f:
            f.write(data)

    def raw(self, relative):
        with open(os.path.join(self.assets, relative), "rb") as f:
            return f.read()

    def quiet(self, function, *args):
        with contextlib.redirect_stdout(io.StringIO()):
            return function(*args)

    def test_check_is_clean_right_after_writing(self):
        self.assertEqual([], checksums.check_lists(self.assets))
        self.assertEqual(0, self.quiet(checksums.run_check, self.assets))

    def test_check_reports_a_changed_model_file_and_writes_nothing(self):
        before = self.raw("models/models.sha256")
        self.put("models/ghoul.glb", b"glb bytes, changed")
        problems = checksums.check_lists(self.assets)
        self.assertEqual(["models/models.sha256: models/ghoul.glb differs (size or sha256)"], problems)
        self.assertEqual(1, self.quiet(checksums.run_check, self.assets))
        self.assertEqual(before, self.raw("models/models.sha256"))

    def test_check_reports_a_missing_list(self):
        os.remove(os.path.join(self.assets, "audio", "audio.sha256"))
        self.assertEqual(["audio/audio.sha256: missing"], checksums.check_lists(self.assets))
        self.assertEqual(1, self.quiet(checksums.run_check, self.assets))
        self.assertFalse(os.path.exists(os.path.join(self.assets, "audio", "audio.sha256")))

    def test_check_reports_an_orphaned_list_file(self):
        self.put("models/old_models.sha256", b"# left over\n")
        self.assertEqual(["models/old_models.sha256: orphaned list, not written by this script"],
                         checksums.check_lists(self.assets))
        self.assertEqual(1, self.quiet(checksums.run_check, self.assets))

    def test_check_reports_a_new_file_without_entry_and_an_entry_without_file(self):
        self.put("textures/floor.png", b"floor")
        os.remove(os.path.join(self.assets, "audio", "hit.wav"))
        self.assertEqual(["textures/textures.sha256: textures/floor.png has no entry",
                          "audio/audio.sha256: entry for audio/hit.wav, but the file is gone"],
                         checksums.check_lists(self.assets))

    def assert_only_form_differs(self, changed):
        """Same entries as the written list, but other text: --check must still fail and name only the form."""
        before = self.raw("models/models.sha256")
        self.assertNotEqual(before, changed)
        self.put("models/models.sha256", changed)
        self.assertEqual(["models/models.sha256: not in the form this script writes"],
                         checksums.check_lists(self.assets))
        self.assertEqual(1, self.quiet(checksums.run_check, self.assets))
        self.assertEqual(changed, self.raw("models/models.sha256"))

    def test_check_reports_a_changed_header(self):
        self.assert_only_form_differs(self.raw("models/models.sha256").replace(b"# sha256", b"#  sha256", 1))

    def test_check_reports_entries_in_another_order(self):
        header, first, second, end = self.raw("models/models.sha256").split(b"\n")
        self.assert_only_form_differs(b"\n".join([header, second, first, end]))

    def test_check_reports_an_extra_blank_line(self):
        self.assert_only_form_differs(self.raw("models/models.sha256") + b"\n")

    def test_a_crlf_checkout_counts_as_current_and_is_not_rewritten(self):
        path = os.path.join(self.assets, "models", "models.sha256")
        crlf = self.raw("models/models.sha256").replace(b"\n", b"\r\n")
        self.put("models/models.sha256", crlf)
        stamp = os.stat(path).st_mtime_ns
        self.assertEqual([], checksums.check_lists(self.assets))
        self.quiet(checksums.write_lists, self.assets)
        self.assertEqual(crlf, self.raw("models/models.sha256"))
        self.assertEqual(stamp, os.stat(path).st_mtime_ns)

    def test_a_real_change_is_written_with_lf_only(self):
        self.put("models/models.sha256", self.raw("models/models.sha256").replace(b"\n", b"\r\n"))
        self.put("models/ghoul.glb", b"new glb")
        self.quiet(checksums.write_lists, self.assets)
        written = self.raw("models/models.sha256")
        self.assertNotIn(b"\r", written)
        self.assertEqual([], checksums.check_lists(self.assets))


if __name__ == "__main__":
    unittest.main()
