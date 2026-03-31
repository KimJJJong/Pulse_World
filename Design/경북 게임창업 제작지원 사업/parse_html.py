import sys
from bs4 import BeautifulSoup

def process_html(filepath, outpath):
    with open(filepath, 'r', encoding='utf-8') as f:
        soup = BeautifulSoup(f, 'lxml')
    
    output = []
    
    # Extract text block by block, processing tables as markdown tables or just row by row
    for elem in soup.body.find_all(['p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'table', 'div'], recursive=False):
        if elem.name == 'table':
            output.append('\n[Table Start]')
            for row in elem.find_all('tr'):
                row_data = []
                for cell in row.find_all(['td', 'th']):
                    # clean up space
                    text = ' '.join(cell.get_text(strip=True).split())
                    row_data.append(text)
                output.append(' | '.join(row_data))
            output.append('[Table End]\n')
        elif elem.name == 'div':
            # recursive extract
            for subelem in elem.find_all(['p', 'table']):
                if subelem.name == 'table':
                    output.append('\n[Table Start]')
                    for row in subelem.find_all('tr'):
                        row_data = []
                        for cell in row.find_all(['td', 'th']):
                            text = ' '.join(cell.get_text(separator=' ', strip=True).split())
                            row_data.append(text)
                        output.append(' | '.join(row_data))
                    output.append('[Table End]\n')
                elif subelem.name == 'p':
                    text = ' '.join(subelem.get_text(strip=True).split())
                    if text:
                        output.append(text)
        else:
            text = ' '.join(elem.get_text(strip=True).split())
            if text:
                output.append(text)
                
    with open(outpath, 'w', encoding='utf-8') as f:
        f.write('\n'.join(output))

if __name__ == "__main__":
    process_html(sys.argv[1], sys.argv[2])
