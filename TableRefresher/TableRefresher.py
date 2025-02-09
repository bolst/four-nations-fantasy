import os
from supabase import create_client, Client
import requests
import time
from datetime import datetime

# create supabase client
url: str = os.environ.get('SupabaseUrl__FNF')
key: str = os.environ.get('SupabaseKeySR__FNF')
supabase: Client = create_client(url, key)

# this gets blocks and hits
def fetch_realtime_stats_from_nhl(ids: list, tournament=False):
    # example url
    # https://api.nhle.com/stats/rest/en/skater/realtime?limit=-1&cayenneExp=seasonId=20242025 and gameTypeId=2 and (playerId=8480036 or playerId=8480039)

    game_type = 19 if tournament else 2

    base_url = f'https://api.nhle.com/stats/rest/en/skater/realtime?limit=-1&cayenneExp=seasonId=20242025 and gameTypeId={game_type} '
    nhl_api_url = base_url + 'and ('

    # loop all player ids except for last one
    for player_id in ids[:-1]:
        nhl_api_url += f'playerId={player_id} or '

    # add last player id not included in loop
    nhl_api_url += f'playerId={ids[-1]})'

    # query nhl api
    response = requests.get(nhl_api_url)

    print(f'NHL returned status code {response.status_code} for realtime stats')
    return response.json()['data'] if response.status_code == 200 else []


# blocks and hits
def update_table_player_realtime_stats(stats, tournament=False):
    for stat in stats:
        entry = {
            'hits': stat['hits'],
            'blocks': stat['blockedShots'],
            'stats_last_updated': str(datetime.now())
        }

        data = format_for_db(entry, tournament)

        response = supabase.table('players').update(data).eq('nhl_id', stat['playerId']).execute()


def fetch_player_stats_from_nhl(ids: list, tournament=False):
    # example url
    # https://api.nhle.com/stats/rest/en/skater/summary?cayenneExp=seasonId=20242025 and playerId=8480039

    game_type = 19 if tournament else 2

    base_url = f'https://api.nhle.com/stats/rest/en/skater/summary?limit=-1&cayenneExp=seasonId=20242025 and gameTypeId={game_type} '
    nhl_api_url = base_url + 'and ('

    # loop all player ids except for last one
    for player_id in ids[:-1]:
        nhl_api_url += f'playerId={player_id} or '

    # add last player id not included in loop
    nhl_api_url += f'playerId={ids[-1]})'

    # query nhl api
    response = requests.get(nhl_api_url)

    print(f'NHL returned status code {response.status_code} for player stats')
    return response.json()['data'] if response.status_code == 200 else []


def update_table_player_stats(stats, tournament=False):
    for stat in stats:
        entry = {
            'goals': stat['goals'],
            'assists': stat['assists'],
            'pp_points': stat['ppPoints'],
            'sh_points': stat['shPoints'],
            'shots_on_goal': stat['shots'],
            'games_played': stat['gamesPlayed'],
            'stats_last_updated': str(datetime.now())
        }

        data = format_for_db(entry, tournament)

        response = supabase.table('players').update(data).eq('nhl_id', stat['playerId']).execute()


def fetch_goalie_stats_from_nhl(ids: list, tournament=False):
    # example url
    # https://api.nhle.com/stats/rest/en/goalie/summary?cayenneExp=seasonId=20242025 and playerId=8480045

    game_type = 19 if tournament else 2

    base_url = f'https://api.nhle.com/stats/rest/en/goalie/summary?cayenneExp=seasonId=20242025 and gameTypeId={game_type} '
    nhl_api_url = base_url + 'and ('

    # loop all player ids except for last one
    for player_id in ids[:-1]:
        nhl_api_url += f'playerId={player_id} or '

    # add last player id not included in loop
    nhl_api_url += f'playerId={ids[-1]})'

    # query nhl api
    response = requests.get(nhl_api_url)

    print(f'NHL returned status code {response.status_code} for goalie stats')
    return response.json()['data'] if response.status_code == 200 else []


def update_table_goalie_stats(stats, tournament=False):
    for stat in stats:
        entry = {
            'goalie_wins': stat['wins'],
            'goalie_gaa': stat['goalsAgainstAverage'],
            'goalie_sv_pctg': stat['savePct'],
            'games_played': stat['gamesPlayed'],
            'goalie_shutouts': stat['shutouts'],
            'goalie_saves': stat['saves'],
            'goalie_goals_against': stat['goalsAgainst'],
            'stats_last_updated': str(datetime.now())
        }

        data = format_for_db(entry, tournament)

        response = supabase.table('players').update(data).eq('nhl_id', stat['playerId']).execute()

def format_for_db(entry: dict, tournament=False):
    data = dict(entry)
    if tournament:
        for key in entry.keys():
            if key not in ['stats_last_updated', 'games_played']:
                data[f'fn_{key}'] = entry[key]
    return data


if __name__ == '__main__':

    # pull players from db
    response = supabase.table('players').select('nhl_id').execute()
    players = response.data

    # get player ids
    player_ids = [player['nhl_id'] for player in players]
    print(f'{len(player_ids)} queried from Supabase')

    # pull games from db
    response = supabase.table('games').select('nhl_id').execute()
    games = [game['nhl_id'] for game in response.data]

    while True:
        # regular season
        realtime_stats = fetch_realtime_stats_from_nhl(player_ids)
        print(f'{len(realtime_stats)} realtime stats queried from NHL')
        update_table_player_realtime_stats(realtime_stats)
        print('------------------\n')

        player_stats = fetch_player_stats_from_nhl(player_ids)
        print(f'{len(player_stats)} players queried from NHL')
        update_table_player_stats(player_stats)
        print('------------------\n')

        goalie_stats = fetch_goalie_stats_from_nhl(player_ids)
        print(f'{len(goalie_stats)} goalies queried from NHL')
        update_table_goalie_stats(goalie_stats)
        print('------------------\n')


        # tournament
        fn_realtime_stats = fetch_realtime_stats_from_nhl(player_ids, tournament=True)
        print(f'{len(fn_realtime_stats)} realtime stats queried from Four Nations')
        update_table_player_realtime_stats(fn_realtime_stats, tournament=True)
        print('------------------\n')

        fn_player_stats = fetch_player_stats_from_nhl(player_ids, tournament=True)
        print(f'{len(fn_player_stats)} players queried from Four Nations')
        update_table_player_stats(fn_player_stats, tournament=True)
        print('------------------\n')

        fn_goalie_stats = fetch_goalie_stats_from_nhl(player_ids, tournament=True)
        print(f'{len(fn_goalie_stats)} goalies queried from Four Nations')
        update_table_goalie_stats(fn_goalie_stats, tournament=True)
        print('------------------\n')

        # sleep for 10 min
        print('\nsleeping...\n')
        time.sleep(60 * 10)