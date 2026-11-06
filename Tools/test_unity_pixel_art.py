"""Verify the native/Unity metadata bridge without changing production assets."""
import json
from pathlib import Path
import unittest
from prepare_unity_pixel_art import convert_manifest

ROOT=Path(__file__).resolve().parent.parent

class PixelManifestTests(unittest.TestCase):
    def test_directional_contract_keeps_atlas_style_pose_and_frame_semantics(self):
        source=json.loads((ROOT/'ArtSource/PixelArt/DirectionalSpriteContract.json').read_text(encoding='utf-8'))
        venue=json.loads((ROOT/'Tools/WebGLTemplate/FastGame/art/venue.json').read_text(encoding='utf-8'))
        original=json.dumps(venue,sort_keys=True)
        result=convert_manifest(venue,source)
        self.assertEqual(json.dumps(venue,sort_keys=True),original)
        self.assertEqual(len(result['atlases']),len(source['atlases']))
        for atlas in result['atlases']:
            self.assertEqual(atlas['resource'],'SimBarPixelArt/'+Path(atlas['file']).stem)
            self.assertEqual({d['id']:d['column'] for d in atlas['directionColumns']},source['atlases'][atlas['id']].get('directionColumns',{}))
        roles={r['id']:r for r in result['roles']}
        self.assertEqual({v['id']:v['row'] for v in roles['guest']['variantStyles']},source['roles']['guest']['variantStyles'])
        actions={a['id']:a for a in roles['guest']['actions']}
        self.assertEqual(actions['walk']['atlasFrames'],['wardrobe','walk'])
        self.assertFalse(actions['walk']['hasRow'])
        serve=next(a for a in roles['bartender']['actions'] if a['id']=='serve')
        self.assertTrue(serve['hasRow']);self.assertEqual(serve['row'],0)
        surf=roles['surfer']['actions'][0]
        self.assertTrue(surf['horizontal']);self.assertEqual((surf['footX'],surf['footY']),(32,32))
        self.assertEqual(roles['barseated']['styles'],['Casual2','Punk'])
        self.assertEqual(len(result['occluders']),len(venue['occluders']))
        self.assertEqual([v['actors'] for v in result['occluders']],[v.get('actors',[]) for v in venue['occluders']])
        self.assertEqual({v['id']:{'feet':[v['feet']['x'],v['feet']['y']],'depth':v['depth'],'hideShadow':v['hideShadow']} for v in result['seatedActors']},venue.get('seatedActors',{}))
        self.assertEqual({v['id']:[v['offset']['x'],v['offset']['y']] for v in result['bandStageFeet']},venue.get('bandStageFeet',{}))
        perform=next(a for a in roles['singer']['actions'] if a['id']=='perform')
        self.assertEqual(perform['frameColumns'],list(range(8 if source.get('version',0)>=4 else 32)));self.assertEqual(perform['fps'],8)
        self.assertEqual({d['id']:d['row'] for d in perform['directionRows']},{'FR':0,'BR':1,'BL':2,'FL':3})
        hug=next(a for a in roles['guest']['actions'] if a['id']=='hug')
        self.assertTrue(hug['progressFrames']);self.assertEqual(hug['frameColumns'],list(range(4,8)) if source['atlases']['guestSocial']['file']=='v4-guest-social.png' else list(range(8)))
        self.assertEqual({d['id']:d['row'] for d in hug['directionRowOffsets']},{'FR':0,'BR':1,'BL':2,'FL':3})

    def test_legacy_single_sheet_retains_rows_and_animation(self):
        result=convert_manifest({}, {'frameWidth':64,'frameHeight':64,'columns':4,'drawHeight':54,'footX':32,'footY':60,
            'roles':{'guest':{'variantRows':[0,1], 'actions':{'walk':{'frameColumns':[1,2]}}}}})
        self.assertEqual(result['defaultAtlas'],'animation');self.assertEqual(result['atlases'],[])
        self.assertEqual(result['roles'][0]['variantRows'],[0,1])
        self.assertEqual(result['roles'][0]['actions'][0]['frameColumns'],[1,2])

if __name__=='__main__':unittest.main()
