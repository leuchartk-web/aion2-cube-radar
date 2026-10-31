import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

class LocalizationTests(unittest.TestCase):
    def test_every_resource_key_used_by_shipped_code_exists(self):
        values = json.loads((ROOT / 'src/CubeRadar.Core/Localization/fr.json').read_text(encoding='utf-8'))
        for path in (ROOT / 'src').rglob('*.cs'):
            for key in re.findall(r'\bT\("([^"\n]+)"', path.read_text(encoding='utf-8')):
                self.assertIn(key, values, (path, key))
                self.assertTrue(values[key].strip())
